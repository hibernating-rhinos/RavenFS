namespace RavenFS.Synchronization
{
	using System;
	using System.Collections.Generic;
	using System.Collections.Specialized;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using Multipart;
	using NLog;
	using RavenFS.Client;
	using RavenFS.Infrastructure;
	using RavenFS.Storage;
	using RavenFS.Util;
	using Rdc;
	using Rdc.Wrapper;

	public class ContentUpdateWorkItem : SynchronizationWorkItem
	{
		private readonly Logger log = LogManager.GetCurrentClassLogger();

		private readonly SigGenerator sigGenerator;
		
		public ContentUpdateWorkItem(string file, TransactionalStorage storage, SigGenerator sigGenerator)
			: base(file, storage)
		{
			this.sigGenerator = sigGenerator;
		}

		public override SynchronizationType SynchronizationType
		{
			get { return SynchronizationType.ContentUpdate; }
		}

		public override Task<SynchronizationReport> Perform(string destination)
		{
			return Task.Factory.StartNew(() =>
			{
				var localMetadata = GetLocalMetadata(FileName);
				AssertLocalFileExistsAndIsNotConflicted(localMetadata);
			    return StartSyncingToAsync(destination, localMetadata);
			}, TaskCreationOptions.LongRunning)
			.Unwrap()
			.ContinueWith(task =>
			{
			    SynchronizationReport report;
			    if (task.Status == TaskStatus.Faulted)
			    {
			        report =
			            new SynchronizationReport
			                {
			                    FileName = FileName,
			                    Exception = task.Exception.ExtractSingleInnerException(),
			                    Type = SynchronizationType.ContentUpdate
			                };

			        log.WarnException(
			            string.Format("Failed to perform a synchronization of a file '{0}' to {1}", FileName,
			                     		destination), report.Exception);
			    }
			    else
			    {
			        report = task.Result;

			        if (report.Exception == null)
			        {
			            log.Debug(
			                "Synchronization of a file '{0}' to {1} has finished. {2} bytes were transfered and {3} bytes copied. Need list length was {4}",
			                FileName, destination, report.BytesTransfered, report.BytesCopied, report.NeedListLength);
			        }
			        else
			        {
			            log.WarnException(
			                string.Format("Synchronization of a file '{0}' to {1} has finished with an exception",
			                     			FileName, destination),
			                report.Exception);
			        }
			    }

			    return report;
			});
		}

		private Task<SynchronizationReport> StartSyncingToAsync(string destination, NameValueCollection sourceMetadata)
		{
			var destinationRavenFileSystemClient = new RavenFileSystemClient(destination);

			return destinationRavenFileSystemClient.GetMetadataForAsync(FileName)
				.ContinueWith(
					getMetadataForAsyncTask =>
						{
							var destinationMetadata = getMetadataForAsyncTask.Result;

							if (destinationMetadata == null)
							{
								// if file doesn't exist on destination server - upload it there
								return UploadTo(destination, FileName, sourceMetadata);
							}

							var conflict = CheckConflictWithDestination(sourceMetadata, destinationMetadata);

							if(conflict != null)
							{
								return ApplyConflictOnDestination(conflict, destination, log);
							}

							var localFileDataInfo = GetLocalFileDataInfo(FileName);

							var signatureRepository = new StorageSignatureRepository(Storage, FileName);
							var remoteSignatureCache = new VolatileSignatureRepository(FileName);
							var localRdcManager = new LocalRdcManager(signatureRepository, Storage, sigGenerator);
							var destinationRdcManager = new RemoteRdcManager(destinationRavenFileSystemClient, signatureRepository,
							                                                 remoteSignatureCache);

							var sourceSignatureManifest = localRdcManager.GetSignatureManifest(localFileDataInfo);

							return destinationRdcManager.SynchronizeSignaturesAsync(localFileDataInfo)
								.ContinueWith(
									task =>
										{
											var destinationSignatureManifest = task.Result;

											if (destinationSignatureManifest.Signatures.Count > 0)
											{
												return SynchronizeTo(remoteSignatureCache, destination,
												                     FileName,
												                     sourceSignatureManifest,
												                     sourceMetadata);
											}
											return UploadTo(destination, FileName, sourceMetadata);
										})
								.Unwrap()
								.ContinueWith(
									synchronizationTask =>
										{
											signatureRepository.Dispose();
											remoteSignatureCache.Dispose();

											return synchronizationTask.Result;
										});
						})
				.Unwrap();
		}

		private Task<SynchronizationReport> SynchronizeTo(ISignatureRepository remoteSignatureRepository, string destinationServerUrl, string fileName, SignatureManifest sourceSignatureManifest, NameValueCollection sourceMetadata)
		{
			var seedSignatureInfo = SignatureInfo.Parse(sourceSignatureManifest.Signatures.Last().Name);
			var sourceSignatureInfo = SignatureInfo.Parse(sourceSignatureManifest.Signatures.Last().Name);

			var localFile = StorageStream.Reading(Storage, fileName);

			IList<RdcNeed> needList;
			using (var signatureRepository = new StorageSignatureRepository(Storage, fileName))
			using (var needListGenerator = new NeedListGenerator(remoteSignatureRepository, signatureRepository))
			{
				needList = needListGenerator.CreateNeedsList(seedSignatureInfo, sourceSignatureInfo);
			}

			return PushByUsingMultipartRequest(destinationServerUrl, fileName, sourceMetadata, localFile, needList, localFile);
		}

		private Task<SynchronizationReport> UploadTo(string destinationServerUrl, string fileName, NameValueCollection localMetadata)
		{
			var sourceFileStream = StorageStream.Reading(Storage, fileName);
			var fileSize = sourceFileStream.Length;

			var onlySourceNeed = new List<RdcNeed>
			               	{
			               		new RdcNeed
			               			{
			               				BlockType = RdcNeedType.Source,
			               				BlockLength = (ulong) fileSize,
			               				FileOffset = 0
			               			}
			               	};

			return PushByUsingMultipartRequest(destinationServerUrl, fileName, localMetadata, sourceFileStream, onlySourceNeed, sourceFileStream);
		}

		private Task<SynchronizationReport> PushByUsingMultipartRequest(string destinationServerUrl, string fileName, NameValueCollection sourceMetadata, Stream sourceFileStream, IList<RdcNeed> needList, params IDisposable[] disposables)
		{
			var multipartRequest = new SynchronizationMultipartRequest(destinationServerUrl, SourceServerId, fileName, sourceMetadata,
																	   sourceFileStream, needList);

			log.Debug("Synchronizing a file '{0}' (ETag {1}) to {2} by using multipart request. Need list length is {3}. ", fileName, FileETag, destinationServerUrl, needList.Count);

			return multipartRequest.PushChangesAsync()
				.ContinueWith(t =>
				{
					foreach (var disposable in disposables)
					{
						disposable.Dispose();
					}

					t.AssertNotFaulted();

					return t.Result;
				});
		}

		private NameValueCollection GetLocalMetadata(string fileName)
		{
			NameValueCollection result = null;
			try
			{
				Storage.Batch(
					accessor =>
					{
						result = accessor.GetFile(fileName, 0, 0).Metadata;
					});
			}
			catch (FileNotFoundException)
			{
				return null;
			}
			return result;
		}

		private DataInfo GetLocalFileDataInfo(string fileName)
		{
			FileAndPages fileAndPages = null;

			try
			{
				Storage.Batch(accessor => fileAndPages = accessor.GetFile(fileName, 0, 0));
			}
			catch (FileNotFoundException)
			{
				return null;
			}
			return new DataInfo
			{
				CreatedAt = Convert.ToDateTime(fileAndPages.Metadata["Last-Modified"]).ToUniversalTime(),
				Length = fileAndPages.TotalSize ?? 0,
				Name = fileAndPages.Name
			};
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != typeof (ContentUpdateWorkItem)) return false;
			return Equals((ContentUpdateWorkItem) obj);
		}

		public bool Equals(ContentUpdateWorkItem other)
		{
			if (ReferenceEquals(null, other)) return false;
			if (ReferenceEquals(this, other)) return true;
			return Equals(other.FileName, FileName) && Equals(other.FileETag, FileETag);
		}

		public override int GetHashCode()
		{
			return (FileName != null ? GetType().Name.GetHashCode() ^ FileName.GetHashCode() ^ FileETag.GetHashCode() : 0);
		}
	}
}