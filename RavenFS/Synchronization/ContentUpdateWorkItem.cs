namespace RavenFS.Synchronization
{
	using System;
	using System.Collections.Generic;
	using System.Collections.Specialized;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using Extensions;
	using Multipart;
	using NLog;
	using RavenFS.Client;
	using RavenFS.Infrastructure;
	using RavenFS.Storage;
	using RavenFS.Util;
	using Rdc;
	using Rdc.Wrapper;
	using FileInfo = Client.FileInfo;

	public class ContentUpdateWorkItem : SynchronizationWorkItem
	{
		private readonly Logger log = LogManager.GetCurrentClassLogger();

		private readonly SigGenerator sigGenerator;
		private DataInfo fileDataInfo;
		
		public ContentUpdateWorkItem(string file, TransactionalStorage storage, SigGenerator sigGenerator)
			: base(file, storage)
		{
			this.sigGenerator = sigGenerator;
		}

		public override SynchronizationType SynchronizationType
		{
			get { return SynchronizationType.ContentUpdate; }
		}

		private DataInfo FileDataInfo
		{
			get { return fileDataInfo ?? (fileDataInfo = GetLocalFileDataInfo(FileName)); }
		}

		public override Task<SynchronizationReport> Perform(string destination)
		{
			return Task.Factory.StartNew(() =>
			{
				AssertLocalFileExistsAndIsNotConflicted(FileMetadata);
			    return StartSyncingToAsync(destination);
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

		private Task<SynchronizationReport> StartSyncingToAsync(string destination)
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
								return UploadTo(destination);
							}

							var conflict = CheckConflictWithDestination(FileMetadata, destinationMetadata);

							if(conflict != null)
							{
								return ApplyConflictOnDestination(conflict, destination, log);
							}

							var signatureRepository = new StorageSignatureRepository(Storage, FileName);
							var remoteSignatureCache = new VolatileSignatureRepository(FileName);
							var localRdcManager = new LocalRdcManager(signatureRepository, Storage, sigGenerator);
							var destinationRdcManager = new RemoteRdcManager(destinationRavenFileSystemClient, signatureRepository,
							                                                 remoteSignatureCache);

							log.Debug("Starting to retrieve signatures of a local file '{0}'.", FileName);
							// first we need to create a local file signatures before we synchronize with remote ones
							var sourceSignatureManifest = localRdcManager.GetSignatureManifest(FileDataInfo);

							log.Debug("Number of a local file '{0}' signatures was {1}.", FileName, sourceSignatureManifest.Signatures.Count);

							return destinationRdcManager.SynchronizeSignaturesAsync(FileDataInfo)
								.ContinueWith(
									task =>
										{
											var destinationSignatureManifest = task.Result;

											if (destinationSignatureManifest.Signatures.Count > 0)
											{
												return destinationRavenFileSystemClient.SearchAsync(string.Format("__fileName:{0}", FileName), pageSize: 1)
													.ContinueWith(t =>
													{
														return SynchronizeTo(remoteSignatureCache, destination, sourceSignatureManifest, t.Result.Files[0], destinationMetadata);
													}).Unwrap();
											}
											return UploadTo(destination);
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

		private Task<SynchronizationReport> SynchronizeTo(ISignatureRepository remoteSignatureRepository, string destinationServerUrl, SignatureManifest sourceSignatureManifest, FileInfo destinationFileInfo, NameValueCollection destinationMetadata)
		{
			var seedSignatureInfo = SignatureInfo.Parse(sourceSignatureManifest.Signatures.Last().Name);
			var sourceSignatureInfo = SignatureInfo.Parse(sourceSignatureManifest.Signatures.Last().Name);

			var localFile = StorageStream.Reading(Storage, FileName);

			IList<RdcNeed> needList;
			using (var signatureRepository = new StorageSignatureRepository(Storage, FileName))
			using (var needListGenerator = new NeedListGenerator(remoteSignatureRepository, signatureRepository))
			{
				needList = needListGenerator.CreateNeedsList(seedSignatureInfo, sourceSignatureInfo);
			}

			return PushByUsingMultipartRequest(destinationServerUrl, destinationFileInfo, destinationMetadata, localFile, needList, localFile);
		}

		private Task<SynchronizationReport> UploadTo(string destinationServerUrl)
		{
			var sourceFileStream = StorageStream.Reading(Storage, FileName);
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

			return PushByUsingMultipartRequest(destinationServerUrl, null, null, sourceFileStream, onlySourceNeed, sourceFileStream);
		}

		private Task<SynchronizationReport> PushByUsingMultipartRequest(string destinationServerUrl, FileInfo destinationFileInfo, NameValueCollection destinationMetadata, Stream sourceFileStream, IList<RdcNeed> needList, params IDisposable[] disposables)
		{
			var transferredChangesType = TransferredChangesType.Bytes;

			if (destinationFileInfo != null && destinationFileInfo.TotalSize != null)
			{
				var sourceFileLength = FileDataInfo.Length;
				transferredChangesType = DetermineChangesTransferType(sourceFileLength, destinationFileInfo.TotalSize.Value,
				                                                      destinationMetadata["Content-MD5"]);
			}

			var multipartRequest = new SynchronizationMultipartRequest(Storage, destinationServerUrl, SourceServerId, FileName, FileMetadata,
																	   sourceFileStream, needList, transferredChangesType);

			var bytesToTransferCount = needList.Where(x => x.BlockType == RdcNeedType.Source).Sum(x => (double) x.BlockLength);
			
			log.Debug(
				"Synchronizing a file '{0}' (ETag {1}) to {2} by using multipart request. Need list length is {3}. Number of bytes that needs to be transfered is {4}",
				FileName, FileETag, destinationServerUrl, needList.Count, bytesToTransferCount);

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

		private TransferredChangesType DetermineChangesTransferType(long sourceFileLength, long destinationFileLength, string destinationFileHash)
		{
			if (sourceFileLength == destinationFileLength) // if file length is the same we can work with entire pages
			{
				return TransferredChangesType.Pages;
			}
			else if (sourceFileLength > destinationFileLength) // need to check if data has been appended
			{
				using (var stream  = StorageStream.Reading(Storage, FileName))
				{
					var fileBeginningStream = new NarrowedStream(stream, 0, destinationFileLength - 1);
					var hashOfTheBeginning = fileBeginningStream.GetMD5Hash();

					if (hashOfTheBeginning == destinationFileHash) // 
					{
						return TransferredChangesType.Pages;
					}
				}
			}

			return TransferredChangesType.Bytes;
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