namespace RavenFS.Synchronization
{
	using System;
	using System.Collections.Generic;
	using System.Collections.Specialized;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using Conflictuality;
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
		private static readonly Logger log = LogManager.GetCurrentClassLogger();

		private readonly TransactionalStorage storage;
		private readonly SigGenerator sigGenerator;
		private readonly ConflictDetector conflictDetector;
		private readonly ConflictResolver conflictResolver;

		public ContentUpdateWorkItem(string file, string sourceServerUrl, TransactionalStorage storage, SigGenerator sigGenerator)
			: base(file, sourceServerUrl)
		{
			this.storage = storage;
			this.sigGenerator = sigGenerator;
			this.conflictDetector = new ConflictDetector();
			this.conflictResolver = new ConflictResolver();
		}

		public override Task<SynchronizationReport> Perform(string destination)
		{
			return StartSyncingToAsync(destination);
		}

		private Task<SynchronizationReport> StartSyncingToAsync(string destination)
		{
			var sourceMetadata = GetLocalMetadata(FileName);

			if (sourceMetadata == null)
			{
				log.Debug("Could not synchronize a file '{0}' because it does not exist");

				return SynchronizationUtils.SynchronizationExceptionReport(FileName, string.Format("File {0} could not be found", FileName));
			}

			if (sourceMetadata.AllKeys.Contains(SynchronizationConstants.RavenSynchronizationConflict))
			{
				log.Debug("Could not synchronize a file '{0}' because it is conflicted");

				return SynchronizationUtils.SynchronizationExceptionReport(FileName, string.Format("File {0} is conflicted", FileName));
			}

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

						var conflict = conflictDetector.Check(destinationMetadata, sourceMetadata);
						var isConflictResolved = conflictResolver.IsResolved(destinationMetadata, conflict);

						// optimization - conflict checking on source side before any changes pushed
						if (conflict != null && !isConflictResolved)
						{
							log.Debug("File '{0}' is in conflict with destination version from {1}. Applying conflict on destination", FileName, destination);

							return destinationRavenFileSystemClient.Synchronization
								.ApplyConflictAsync(FileName, conflict.Current.Version, conflict.Remote.ServerId)
								.ContinueWith(task =>
								{
									if (task.Exception != null)
									{
										log.WarnException(string.Format("Failed to apply conflict on {0} for file '{1}'", destination, FileName),
										                  task.Exception.ExtractSingleInnerException());
									}
									return new SynchronizationReport
									{
										FileName = FileName,
										Exception = new SynchronizationException(string.Format("File {0} is conflicted.", FileName)),
										Type = SynchronizationType.ContentUpdate
									};
								});
						}

						var localFileDataInfo = GetLocalFileDataInfo(FileName);

						var signatureRepository = new StorageSignatureRepository(storage, FileName);
						var remoteSignatureCache = new VolatileSignatureRepository(FileName);
						var localRdcManager = new LocalRdcManager(signatureRepository, storage, sigGenerator);
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
							string.Format("Failed to perform a synchronization of a file '{0}' to {1}", FileName, destination), report.Exception);
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
								string.Format("Synchronization of a file '{0}' to {1} has finished with an exception", FileName, destination),
								report.Exception);
						}
					}

					return report;
				});
		}

		private Task<SynchronizationReport> SynchronizeTo(ISignatureRepository remoteSignatureRepository, string destinationServerUrl, string fileName, SignatureManifest sourceSignatureManifest, NameValueCollection sourceMetadata)
		{
			var seedSignatureInfo = SignatureInfo.Parse(sourceSignatureManifest.Signatures.Last().Name);
			var sourceSignatureInfo = SignatureInfo.Parse(sourceSignatureManifest.Signatures.Last().Name);

			var localFile = StorageStream.Reading(storage, fileName);

			IList<RdcNeed> needList;
			using (var signatureRepository = new StorageSignatureRepository(storage, fileName))
			using (var needListGenerator = new NeedListGenerator(remoteSignatureRepository, signatureRepository))
			{
				needList = needListGenerator.CreateNeedsList(seedSignatureInfo, sourceSignatureInfo);
			}

			return PushByUsingMultipartRequest(destinationServerUrl, fileName, sourceMetadata, localFile, needList, localFile);
		}

		private Task<SynchronizationReport> UploadTo(string destinationServerUrl, string fileName, NameValueCollection localMetadata)
		{
			var sourceFileStream = StorageStream.Reading(storage, fileName);
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
			var multipartRequest = new SynchronizationMultipartRequest(destinationServerUrl, SourceServerUrl, fileName, sourceMetadata,
																	   sourceFileStream, needList);

			log.Debug("Synchronizing a file '{0}' to {1} by using multipart request. Need list length is {2}", fileName, destinationServerUrl, needList.Count);

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
				storage.Batch(
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
				storage.Batch(accessor => fileAndPages = accessor.GetFile(fileName, 0, 0));
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
			return Equals(other.FileName, FileName);
		}

		public override int GetHashCode()
		{
			return (FileName != null ? FileName.GetHashCode() : 0);
		}
	}
}