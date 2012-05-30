using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using RavenFS.Extensions;
using RavenFS.Infrastructure;
using RavenFS.Notifications;
using RavenFS.Storage;
using System.Linq;
using RavenFS.Util;

namespace RavenFS.Controllers
{
	using Synchronization;

	public class FilesController : RavenController
	{
		public List<FileHeader> Get()
		{
			List<FileHeader> fileHeaders = null;
			Storage.Batch(accessor =>
			              	{
			              		fileHeaders =
			              			accessor.ReadFiles(Paging.Start, Paging.PageSize).Where(
			              				x => !x.Metadata.AllKeys.Contains(SynchronizationConstants.RavenDeleteMarker)).ToList();
			              	});
			return fileHeaders;
		}

		public HttpResponseMessage Get(string name)
		{
			name = Uri.UnescapeDataString(name);
			FileAndPages fileAndPages = null;
			try
			{
				Storage.Batch(accessor => fileAndPages = accessor.GetFile(name, 0, 0));
			}
			catch (FileNotFoundException)
			{
				throw new HttpResponseException(HttpStatusCode.NotFound);
			}

			if (fileAndPages.Metadata.AllKeys.Contains(SynchronizationConstants.RavenDeleteMarker))
			{
				throw new HttpResponseException(HttpStatusCode.NotFound);
			}

			var ravenReadOnlyStream = new RavenReadOnlyStream(Storage, BufferPool, name);
			var result = StreamResult(name, ravenReadOnlyStream);
			MetadataExtensions.AddHeaders(result, fileAndPages);
			return result;
		}

		public HttpResponseMessage Delete(string name)
		{
			name = Uri.UnescapeDataString(name);

			Storage.Batch(accessor =>
			{
				AssertFileIsNotBeingSynced(name, accessor);
			    accessor.Delete(name);
				var tombstoneMetadata = new NameValueCollection {{SynchronizationConstants.RavenDeleteMarker, "true"}};
				HistoryUpdater.UpdateLastModified(tombstoneMetadata);
				accessor.PutFile(name, 0, tombstoneMetadata);
			});

			Search.Delete(name);

			Publisher.Publish(new FileChange { File = name, Action = FileChangeAction.Delete });
			SynchronizationTask.ProcessWork(new DeleteWorkItem(name, RavenFileSystem.ServerUrl, Storage));

			return new HttpResponseMessage(HttpStatusCode.NoContent);
		}

		[AcceptVerbs("HEAD")]
		public HttpResponseMessage Head(string name)
		{
			name = Uri.UnescapeDataString(name);
			FileAndPages fileAndPages = null;
			try
			{
				Storage.Batch(accessor => fileAndPages = accessor.GetFile(name, 0, 0));
			}
			catch (FileNotFoundException)
			{
				return new HttpResponseMessage(HttpStatusCode.NotFound);
			}

			var httpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK);
			httpResponseMessage.Content = new HttpMessageContent(httpResponseMessage);
			MetadataExtensions.AddHeaders(httpResponseMessage, fileAndPages);
			return httpResponseMessage;
		}

		public HttpResponseMessage Post(string name)
		{
			name = Uri.UnescapeDataString(name);

			var headers = Request.Headers.FilterHeaders();
			HistoryUpdater.UpdateLastModified(headers);
			HistoryUpdater.Update(name, headers);
			try
			{
				Storage.Batch(accessor =>
				{
				    AssertFileIsNotBeingSynced(name, accessor);
				    accessor.UpdateFileMetadata(name, headers);
				});

				Search.Index(name, headers);

				Publisher.Publish(new FileChange {File = name, Action = FileChangeAction.Update});
				SynchronizationTask.ProcessWork(new MetadataUpdateWorkItem(name, headers, RavenFileSystem.ServerUrl));
			}
			catch (FileNotFoundException)
			{
				return new HttpResponseMessage(HttpStatusCode.NotFound);
			}

			return new HttpResponseMessage(HttpStatusCode.NoContent);
		}

		[AcceptVerbs("PATCH")]
		public HttpResponseMessage Patch(string name, string rename)
		{
			try
			{
				FileAndPages fileAndPages = null;
				Storage.Batch(accessor =>
				{
					AssertFileIsNotBeingSynced(name, accessor);
					fileAndPages = accessor.GetFile(name, 0, 0);
					accessor.RenameFile(name, rename);
				});

				Search.Delete(name);
				Search.Index(rename, fileAndPages.Metadata);
				Publisher.Publish(new FileChange { File = name, Action = FileChangeAction.Renaming });
				Publisher.Publish(new FileChange { File = rename, Action = FileChangeAction.Renamed });
				//TODO SynchronizationTask.ProcessWork(fileRenamed);
			}
			catch (FileNotFoundException)
			{
				return new HttpResponseMessage(HttpStatusCode.NotFound);
			}

			return new HttpResponseMessage(HttpStatusCode.NoContent);
		}

		public Task<HttpResponseMessage> Put(string name)
		{
			name = Uri.UnescapeDataString(name);

			var headers = Request.Headers.FilterHeaders();
			HistoryUpdater.UpdateLastModified(headers);
			HistoryUpdater.Update(name, headers);
			name = Uri.UnescapeDataString(name);
			Storage.Batch(accessor =>
			{
				AssertFileIsNotBeingSynced(name, accessor);
				accessor.Delete(name);

				long? contentLength = Request.Content.Headers.ContentLength;
				if (Request.Headers.TransferEncodingChunked ?? false)
				{
					contentLength = null;
				}
				accessor.PutFile(name, contentLength, headers);

				Search.Index(name, headers);
			});

			return Request.Content.ReadAsStreamAsync()
				.ContinueWith(task =>
				{
					var readFileToDatabase = new ReadFileToDatabase(BufferPool, Storage, task.Result, name);
					return readFileToDatabase.Execute()
						.ContinueWith(readingTask =>
						{
							HistoryUpdater.UpdateLastModified(headers);// update with the final file size

							using (var stream = StorageStream.Reading(Storage, name))
							{
							    headers["Content-MD5"] = stream.GetMD5Hash();
							}

							Storage.Batch(accessor => accessor.UpdateFileMetadata(name, headers));
							headers["Content-Length"] = readFileToDatabase.TotalSizeRead.ToString(CultureInfo.InvariantCulture);
							Search.Index(name, headers);
							Publisher.Publish(new FileChange { Action = FileChangeAction.Add, File = name });
							SynchronizationTask.ProcessWork(new ContentUpdateWorkItem(name, RavenFileSystem.ServerUrl, Storage, SigGenerator));
							readFileToDatabase.Dispose();
							return readingTask;
						})
						.Unwrap();
				})
				.Unwrap()
				.ContinueWith(task =>
				{
					task.AssertNotFaulted();

					return new HttpResponseMessage(HttpStatusCode.Created);
				});
		}

		private class ReadFileToDatabase : IDisposable
		{
			private readonly BufferPool bufferPool;
			private readonly TransactionalStorage storage;
			private readonly string filename;
			private int pos;
			readonly byte[] buffer;
			private readonly Stream inputStream;
			public int TotalSizeRead;
			public ReadFileToDatabase(BufferPool bufferPool, TransactionalStorage storage, Stream inputStream, string filename)
			{
				this.bufferPool = bufferPool;
				this.inputStream = inputStream;
				this.storage = storage;
				this.filename = filename;
				buffer = bufferPool.TakeBuffer(64 * 1024);
			}

			public Task Execute()
			{
				return inputStream.ReadAsync(buffer)
					.ContinueWith(task =>
					{
						TotalSizeRead += task.Result;

						if (task.Result == 0) // nothing left to read
						{
							storage.Batch(accessor => accessor.CompleteFileUpload(filename));
							return task; // task is done
						}

						storage.Batch(accessor =>
						{
							var hashKey = accessor.InsertPage(buffer, task.Result);
							accessor.AssociatePage(filename, hashKey, pos, task.Result);
						});

						pos++;
						return Execute();
					})
					.Unwrap();
			}

			public void Dispose()
			{
				bufferPool.ReturnBuffer(buffer);
			}
		}

		private class FileAccessTool
		{
			private readonly TransactionalStorage storage;
			private readonly BufferPool bufferPool;
			private const int PagesBatchSize = 64;

			public FileAccessTool(TransactionalStorage storage, BufferPool bufferPool)
			{
				this.storage = storage;
				this.bufferPool = bufferPool;
			}

			public Task<object> WriteFile(Stream output, string filename, int fromPage, long? maybeRange)
			{
				FileAndPages fileAndPages = null;
				storage.Batch(accessor => fileAndPages = accessor.GetFile(filename, fromPage, PagesBatchSize));
				if (fileAndPages.Pages.Count == 0)
				{
					return Task.Factory.StartNew(() => new object());
				}

				var offset = 0;
				var pageIndex = 0;
				if (maybeRange != null)
				{
					var range = maybeRange.Value;
					foreach (var page in fileAndPages.Pages)
					{
						if (page.Size > range)
						{
							offset = (int)range;
							break;
						}
						range -= page.Size;
						pageIndex++;
					}

					if (pageIndex >= fileAndPages.Pages.Count)
					{
						return WriteFile(output, filename, fromPage + fileAndPages.Pages.Count, range);
					}
				}

				return WritePages(output, fileAndPages.Pages, pageIndex, offset)
					.ContinueWith(task =>
					{
						task.AssertNotFaulted();

						return WriteFile(output, filename, fromPage + fileAndPages.Pages.Count, null);
					}).Unwrap();
			}

			private Task WritePages(Stream output, List<PageInformation> pages, int index, int offset)
			{
				return WritePage(output, pages[index], offset)
					.ContinueWith(task =>
					{
						if (task.Exception != null)
							return task;

						if (index + 1 >= pages.Count)
							return task;

						return WritePages(output, pages, index + 1, 0);
					})
					.Unwrap();
			}

			private Task WritePage(Stream output, PageInformation information, int offset)
			{
				var buffer = bufferPool.TakeBuffer(information.Size);
				try
				{
					storage.Batch(accessor => accessor.ReadPage(information.Id, buffer));
					return output.WriteAsync(buffer, offset, information.Size - offset)
						.ContinueWith(task =>
						{
							bufferPool.ReturnBuffer(buffer);
							return task;
						})
						.Unwrap();
				}
				catch (Exception)
				{
					bufferPool.ReturnBuffer(buffer);
					throw;
				}
			}
		}
	}
}