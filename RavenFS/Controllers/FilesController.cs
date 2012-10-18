using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using RavenFS.Extensions;
using RavenFS.Storage;
using RavenFS.Util;

namespace RavenFS.Controllers
{
	using System.Security.Cryptography;
	using System.Threading;
	using Client;
	using NLog;
	using Synchronization;

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

			ConcurrencyAwareExecutor.Execute(() => Storage.Batch(accessor =>
			{
				AssertFileIsNotBeingSynced(name, accessor, true);
				StorageOperationsTask.IndicateFileToDelete(name);

				if(!name.EndsWith(RavenFileNameHelper.DownloadingFileSuffix)) // don't create a tombstone for .downloading file
				{
					var tombstoneMetadata = new NameValueCollection().WithDeleteMarker();
					Historian.UpdateLastModified(tombstoneMetadata);
					accessor.PutFile(name, 0, tombstoneMetadata, true);
				}
			}), ConcurrencyResponseException);

			Publisher.Publish(new FileChange { File = name, Action = FileChangeAction.Delete });
			log.Debug("File '{0}' was deleted", name);
			
			StartSynchronizeDestinationsInBackground();

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

			if(fileAndPages.Metadata.AllKeys.Contains(SynchronizationConstants.RavenDeleteMarker))
			{
				log.Debug("Cannot get metadata of a file '{0}' because file was deleted", name);
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

			try
			{

				ConcurrencyAwareExecutor.Execute(() =>
				Storage.Batch(accessor =>
				{
					AssertFileIsNotBeingSynced(name, accessor, true);
					accessor.UpdateFileMetadata(name, headers);
				}), ConcurrencyResponseException);
			}
			catch (FileNotFoundException)
			{
				log.Debug("Cannot update metadata because file '{0}' was not found", name);
				return new HttpResponseMessage(HttpStatusCode.NotFound);
			}

			Search.Index(name, headers);

			Publisher.Publish(new FileChange { File = name, Action = FileChangeAction.Update });

			StartSynchronizeDestinationsInBackground();

			log.Debug("Metadata of a file '{0}' was updated", name);
			return new HttpResponseMessage(HttpStatusCode.NoContent);
		}

		[AcceptVerbs("PATCH")]
		public HttpResponseMessage Patch(string name, string rename)
		{
			try
			{
				ConcurrencyAwareExecutor.Execute(() =>
				Storage.Batch(accessor =>
				{
					AssertFileIsNotBeingSynced(name, accessor, true);
					
					var existingHeader = accessor.ReadFile(rename);
					if (existingHeader != null && !existingHeader.Metadata.AllKeys.Contains(SynchronizationConstants.RavenDeleteMarker))
					{
						throw new InvalidOperationException("Cannot rename because file " + rename + " already exists");
					}

					var metadata = accessor.GetFile(name, 0, 0).Metadata;
					Historian.UpdateLastModified(metadata);

					var operation = new RenameFileOperation()
						                          {
							                          Name = name,
													  Rename = rename,
													  MetadataAfterOperation = metadata
						                          };

					accessor.SetConfigurationValue(RavenFileNameHelper.RenameOperationConfigNameForFile(name), operation);
					accessor.PulseTransaction(); // commit rename operation config

					StorageOperationsTask.RenameFile(operation);
				}), ConcurrencyResponseException);
			}
			catch (FileNotFoundException)
			{
				log.Debug("Cannot rename a file '{0}' to '{1}' because a file was not found", name, rename);
				return new HttpResponseMessage(HttpStatusCode.NotFound);
			}
			
			log.Debug("File '{0}' was renamed to '{1}'", name, rename);

			StartSynchronizeDestinationsInBackground();

			return new HttpResponseMessage(HttpStatusCode.NoContent);
		}

		public async Task<HttpResponseMessage> Put(string name, string uploadId = null)
		{
			try
			{
				name = Uri.UnescapeDataString(name);

				var headers = Request.Headers.FilterHeaders();
				Historian.UpdateLastModified(headers);
				Historian.Update(name, headers);

				SynchronizationTask.Cancel(name);

				ConcurrencyAwareExecutor.Execute(() => Storage.Batch(accessor =>
				{
					AssertFileIsNotBeingSynced(name, accessor, true);
					StorageOperationsTask.IndicateFileToDelete(name);

					long? contentLength = Request.Content.Headers.ContentLength;
					if (Request.Headers.TransferEncodingChunked ?? false)
					{
						contentLength = null;
					}
					accessor.PutFile(name, contentLength, headers);

					Search.Index(name, headers);
				}));

				log.Debug("Inserted a new file '{0}' with ETag {1}", name, headers.Value<Guid>("ETag"));

				var contentStream = await Request.Content.ReadAsStreamAsync();

				using (var readFileToDatabase = new ReadFileToDatabase(BufferPool, Storage, contentStream, name))
				{
					await readFileToDatabase.Execute();

					Historian.UpdateLastModified(headers); // update with the final file size

					log.Debug("File '{0}' was uploaded. Starting to update file medatata and indexes", name);

					headers["Content-MD5"] = readFileToDatabase.FileHash;

					Storage.Batch(accessor => accessor.UpdateFileMetadata(name, headers));
					headers["Content-Length"] = readFileToDatabase.TotalSizeRead.ToString(CultureInfo.InvariantCulture);
					Search.Index(name, headers);
					Publisher.Publish(new FileChange { Action = FileChangeAction.Add, File = name });

					log.Debug("Updates of '{0}' metadata and indexes were finished. New file ETag is {1}", name,
							  headers.Value<Guid>("ETag"));

					StartSynchronizeDestinationsInBackground();
				}
			}
			catch (Exception ex)
			{
				if (uploadId != null)
				{
					Guid uploadIdentifier;
					if (Guid.TryParse(uploadId, out uploadIdentifier))
					{
						Publisher.Publish(new UploadFailed() { UploadId = uploadIdentifier, File = name });
					}
				}

				log.WarnException(string.Format("Failed to upload a file '{0}'", name), ex);

				var concurrencyException = ex as ConcurrencyException;
				if (concurrencyException != null)
				{
					throw ConcurrencyResponseException(concurrencyException);
				}

				throw;
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

						ConcurrencyAwareExecutor.Execute(() => storage.Batch(accessor =>
						{
							var hashKey = accessor.InsertPage(buffer, task.Result);
							accessor.AssociatePage(filename, hashKey, pos, task.Result);
						}));
						
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

		private void StartSynchronizeDestinationsInBackground()
		{
			Task.Factory.StartNew(() => SynchronizationTask.SynchronizeDestinationsAsync()
				.ContinueWith(t => t.AssertNotFaulted()), CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);
		}
	}
}
