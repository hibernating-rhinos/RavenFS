namespace RavenFS.Rdc
{
	using System;
	using System.Collections.Generic;
	using System.Collections.Specialized;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using Client;
	using Conflictuality;
	using Infrastructure;
	using Multipart;
	using Storage;
	using Util;
	using Wrapper;

	public class ContentUpdateWorkItem : SynchronizationWorkItem
	{
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
				return SynchronizationUtils.SynchronizationExceptionReport(string.Format("File {0} could not be found", FileName));
			}

			if (sourceMetadata.AllKeys.Contains(SynchronizationConstants.RavenReplicationConflict))
			{
				return SynchronizationUtils.SynchronizationExceptionReport(string.Format("File {0} is conflicted", FileName));
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
							return destinationRavenFileSystemClient.Synchronization
								.ApplyConflictAsync(FileName, conflict.Current.Version, conflict.Remote.ServerId)
								.ContinueWith(task =>
								{
									task.AssertNotFaulted();
									return new SynchronizationReport()
									{
										Exception = new SynchronizationException(string.Format("File {0} is conflicted.", FileName))
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
								Exception = task.Exception.ExtractSingleInnerException()
							};
					}
					else
					{
						report = task.Result;
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