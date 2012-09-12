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
using RavenFS.Storage;
using System.Linq;
using RavenFS.Util;

namespace RavenFS.Controllers
{
	using System.Security.Cryptography;
	using System.Text;
	using Client;
	using NLog;
	using Synchronization;
	using FileChange = Notifications.FileChange;
	using FileChangeAction = Notifications.FileChangeAction;

	public class FilesController : RavenController
	{
		private static readonly Logger log = LogManager.GetCurrentClassLogger();

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
				log.Debug("File '{0}' was not found", name);
				throw new HttpResponseException(HttpStatusCode.NotFound);
			}

			if (fileAndPages.Metadata.AllKeys.Contains(SynchronizationConstants.RavenDeleteMarker))
			{
				log.Debug("File '{0}' is not accessible to get (Raven-Delete-Marker set)", name);
				throw new HttpResponseException(HttpStatusCode.NotFound);
			}

			var readingStream = StorageStream.Reading(Storage,name);
			var result = StreamResult(name, readingStream);
			MetadataExtensions.AddHeaders(result, fileAndPages);
			return result;
		}

		public HttpResponseMessage Delete(string name)
		{
			name = Uri.UnescapeDataString(name);
			var shouldRetry = false;
			var retries = 128;

			do
			{
				try
				{
					Storage.Batch(accessor =>
					{
						AssertFileIsNotBeingSynced(name, accessor);
						accessor.Delete(name);
						var tombstoneMetadata = new NameValueCollection {{SynchronizationConstants.RavenDeleteMarker, "true"}};
						Historian.UpdateLastModified(tombstoneMetadata);
						accessor.PutFile(name, 0, tombstoneMetadata);
					});
				}
				catch (ConcurrencyException ce)
				{
					if (retries-- > 0)
					{
						shouldRetry = true;
						continue;
					}
					throw ConcurrencyResponseException(ce);
				}
			} while (shouldRetry);

			Search.Delete(name);

			Publisher.Publish(new FileChange { File = name, Action = FileChangeAction.Delete });
			log.Debug("File '{0}' was deleted", name);
			SynchronizationTask.SynchronizeDestinationsAsync();

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
				log.Debug("Cannot get metadata of a file '{0}' because file was not found", name);
				return new HttpResponseMessage(HttpStatusCode.NotFound);
			}

			var httpResponseMessage = Request.CreateResponse(HttpStatusCode.OK, fileAndPages);
			MetadataExtensions.AddHeaders(httpResponseMessage, fileAndPages);
			return httpResponseMessage;
		}

		public HttpResponseMessage Post(string name)
		{
			name = Uri.UnescapeDataString(name);

			var headers = Request.Headers.FilterHeaders();
			Historian.UpdateLastModified(headers);
			Historian.Update(name, headers);
			
			var shouldRetry = false;
			var retries = 128;

			do
			{
				try
				{
					Storage.Batch(accessor =>
					{
						AssertFileIsNotBeingSynced(name, accessor);
						accessor.UpdateFileMetadata(name, headers);
					});

					Search.Index(name, headers);

					Publisher.Publish(new FileChange {File = name, Action = FileChangeAction.Update});
					SynchronizationTask.SynchronizeDestinationsAsync();
				}
				catch (FileNotFoundException)
				{
					log.Debug("Cannot update metadata because file '{0}' was not found", name);
					return new HttpResponseMessage(HttpStatusCode.NotFound);
				}
				catch (ConcurrencyException ce)
				{
					if (retries-- > 0)
					{
						shouldRetry = true;
						continue;
					}
					throw ConcurrencyResponseException(ce);
				}
			} while (shouldRetry);

			log.Debug("Metadata of a file '{0}' was updated", name);
			return new HttpResponseMessage(HttpStatusCode.NoContent);
		}

		[AcceptVerbs("PATCH")]
		public HttpResponseMessage Patch(string name, string rename)
		{
			FileAndPages fileAndPages = null;
			var shouldRetry = false;
			var retries = 128;

			do
			{
				try
				{
					Storage.Batch(accessor =>
					{
						AssertFileIsNotBeingSynced(name, accessor);
						fileAndPages = accessor.GetFile(name, 0, 0);

						var metadata = fileAndPages.Metadata;
						Historian.UpdateLastModified(metadata);

						// copy renaming file metadata and set special markers
						var tombstoneMetadata = new NameValueCollection(metadata)
						                        	{
						                        		{SynchronizationConstants.RavenDeleteMarker, "true"},
						                        		{SynchronizationConstants.RavenRenameFile, rename}
						                        	};

						accessor.RenameFile(name, rename);
						accessor.UpdateFileMetadata(rename, metadata);
						accessor.PutFile(name, 0, tombstoneMetadata);
					});
				}
				catch (FileNotFoundException)
				{
					log.Debug("Cannot rename a file '{0}' to '{1}' because a file was not found", name, rename);
					return new HttpResponseMessage(HttpStatusCode.NotFound);
				}
				catch (ConcurrencyException ce)
				{
					if (retries-- > 0)
					{
						shouldRetry = true;
						continue;
					}
					throw ConcurrencyResponseException(ce);
				}
			} while (shouldRetry);

			Search.Delete(name);
			Search.Index(rename, fileAndPages.Metadata);
			Publisher.Publish(new FileChange { File = name, Action = FileChangeAction.Renaming });
			Publisher.Publish(new FileChange { File = rename, Action = FileChangeAction.Renamed });

			log.Debug("File '{0}' was renamed to '{1}'", name, rename);

			SynchronizationTask.SynchronizeDestinationsAsync();

			return new HttpResponseMessage(HttpStatusCode.NoContent);
		}

		public async Task<HttpResponseMessage> Put(string name)
		{
			name = Uri.UnescapeDataString(name);

			var headers = Request.Headers.FilterHeaders();
			Historian.UpdateLastModified(headers);
			Historian.Update(name, headers);
			name = Uri.UnescapeDataString(name);
			
			var shouldRetry = false;
			var retries = 128;

			do
			{
				try
				{
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
				}
				catch (ConcurrencyException ce)
				{
					if (retries-- > 0)
					{
						shouldRetry = true;
						continue;
					}
					throw ConcurrencyResponseException(ce);
				}
			} while (shouldRetry);

			log.Debug("Inserted a new file '{0}' with ETag {1}", name, headers.Value<Guid>("ETag"));

			var contentStream = await Request.Content.ReadAsStreamAsync();

			using (var readFileToDatabase = new ReadFileToDatabase(BufferPool, Storage, contentStream, name))
			{
				try
				{
					await readFileToDatabase.Execute();

					Historian.UpdateLastModified(headers); // update with the final file size

					log.Debug("File '{0}' was uploaded. Starting to update file medatata and indexes", name);

					headers["Content-MD5"] = readFileToDatabase.FileHash;

					Storage.Batch(accessor => accessor.UpdateFileMetadata(name, headers));
					headers["Content-Length"] = readFileToDatabase.TotalSizeRead.ToString(CultureInfo.InvariantCulture);
					Search.Index(name, headers);
					Publisher.Publish(new FileChange {Action = FileChangeAction.Add, File = name});

					log.Debug("Updates of '{0}' metadata and indexes were finished. New file ETag is {1}", name,
					          headers.Value<Guid>("ETag"));

					SynchronizationTask.SynchronizeDestinationsAsync();
				}
				catch (Exception ex)
				{
					log.WarnException(string.Format("Failed to upload a file '{0}'", name), ex);

					var concurrencyException = ex as ConcurrencyException;
					if (concurrencyException != null)
					{
						throw ConcurrencyResponseException(concurrencyException);
					}

					throw;
				}
			}

			return new HttpResponseMessage(HttpStatusCode.Created);
		}

		private class ReadFileToDatabase : IDisposable
		{
			private readonly BufferPool bufferPool;
			private readonly TransactionalStorage storage;
			private readonly string filename;
			private readonly MD5 md5Hasher;
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
				buffer = bufferPool.TakeBuffer(StorageConstants.MaxPageSize);
				md5Hasher = new MD5CryptoServiceProvider();
			}

			public string FileHash { get; private set; }

			public Task Execute()
			{
				return inputStream.ReadAsync(buffer)
					.ContinueWith(task =>
					{
						TotalSizeRead += task.Result;

						if (task.Result == 0) // nothing left to read
						{
							storage.Batch(accessor => accessor.CompleteFileUpload(filename));
							md5Hasher.TransformFinalBlock(new byte[0], 0, 0);

							FileHash = md5Hasher.Hash.ToStringHash();

							return task; // task is done
						}

						storage.Batch(accessor =>
						{
							var hashKey = accessor.InsertPage(buffer, task.Result);
							accessor.AssociatePage(filename, hashKey, pos, task.Result);
						});

						md5Hasher.TransformBlock(buffer, 0, task.Result, null, 0);


						pos++;
						return Execute();
					})
					.Unwrap();
			}

			public void Dispose()
			{
				bufferPool.ReturnBuffer(buffer);
				md5Hasher.Dispose();
			}
		}
	}
}
