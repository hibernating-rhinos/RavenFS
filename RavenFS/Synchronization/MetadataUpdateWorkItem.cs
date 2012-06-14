namespace RavenFS.Synchronization
{
	using System;
	using System.Collections.Specialized;
	using System.IO;
	using System.Linq;
	using System.Net;
	using System.Threading.Tasks;
	using Conflictuality;
	using Multipart;
	using Newtonsoft.Json;
	using NLog;
	using RavenFS.Client;
	using RavenFS.Extensions;

	public class MetadataUpdateWorkItem : SynchronizationWorkItem
	{
		private static readonly Logger log = LogManager.GetCurrentClassLogger();

		private readonly NameValueCollection sourceMetadata;
		private readonly NameValueCollection destinationMetadata;
		private readonly ConflictDetector conflictDetector;
		private readonly ConflictResolver conflictResolver;

		public MetadataUpdateWorkItem(string fileName, NameValueCollection sourceMetadata, NameValueCollection destinationMetadata, string sourceUrl) : base(fileName, sourceUrl)
		{
			this.sourceMetadata = sourceMetadata;
			this.destinationMetadata = destinationMetadata;
			this.conflictDetector = new ConflictDetector();
			this.conflictResolver = new ConflictResolver();
		}

		public override Task<SynchronizationReport> Perform(string destination)
		{
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

			var conflict = conflictDetector.Check(destinationMetadata, sourceMetadata);
			var isConflictResolved = conflictResolver.IsResolved(destinationMetadata, conflict);

			// optimization - conflict checking on source side before any changes pushed
			if (conflict != null && !isConflictResolved)
			{
				log.Debug("File '{0}' is in conflict with destination version from {1}. Applying conflict on destination", FileName, destination);

				var destinationRavenFileSystemClient = new RavenFileSystemClient(destination);

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
							Type = SynchronizationType.MetadataUpdate
						};
					});
			}

			var request = (HttpWebRequest)WebRequest.Create(destination + "/synchronization/updatemetadata/" + FileName);

			request.Method = "POST";
			request.ContentLength = 0;
			request.AddHeaders(sourceMetadata);

			request.Headers[SyncingMultipartConstants.SourceServerUrl] = SourceServerUrl;

			return request
				.GetResponseAsync()
				.ContinueWith(task =>
					{
						try
						{
							using (var stream = task.Result.GetResponseStream())
							{
								return new JsonSerializer().Deserialize<SynchronizationReport>(new JsonTextReader(new StreamReader(stream)));
							}
						}
						catch (AggregateException e)
						{
							var report = new SynchronizationReport
							             	{
							             		FileName = FileName
							             	};

							var singleException = e.ExtractSingleInnerException();
							var we = singleException as WebException;
							if (we == null)
							{
								report.Exception = singleException;
							}
							else if(we.Response is HttpWebResponse)
							{
								var httpWebResponse = (HttpWebResponse) we.Response;
								if (httpWebResponse.StatusCode == HttpStatusCode.PreconditionFailed)
								{
									using (var stream = httpWebResponse.GetResponseStream())
									{
										report.Exception = new JsonSerializer().Deserialize<SynchronizationException>(new JsonTextReader(new StreamReader(stream)));
									}
								}
								else
								{
									using (var stream = httpWebResponse.GetResponseStream())
									using (var reader = new StreamReader(stream))
									{
										throw new InvalidOperationException(reader.ReadToEnd());
									}
								}
							}
							else
							{
								report.Exception = singleException;
							}

							return report;
						}
					});
		}
	}
}