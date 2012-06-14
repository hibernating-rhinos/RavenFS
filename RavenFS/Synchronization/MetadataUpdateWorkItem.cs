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

		public MetadataUpdateWorkItem(string fileName, NameValueCollection sourceMetadata, NameValueCollection destinationMetadata, string sourceUrl)
			: base(fileName, sourceUrl)
		{
			this.sourceMetadata = sourceMetadata;
			this.destinationMetadata = destinationMetadata;
		}

		public override Task<SynchronizationReport> Perform(string destination)
		{
			return Task.Factory.StartNew(() =>
			{
			    AssertLocalFileExistsAndIsNotConflicted(sourceMetadata);
				
				var conflict = GetConflictWithDestination(sourceMetadata, destinationMetadata);

				if (conflict != null)
				{
					log.Debug("File '{0}' is in conflict with destination version from {1}. Applying conflict on destination", FileName, destination);

					var destinationRavenFileSystemClient = new RavenFileSystemClient(destination);

					return destinationRavenFileSystemClient.Synchronization
						.ApplyConflictAsync(FileName, conflict.Current.Version, conflict.Remote.ServerId)
						.ContinueWith(task =>
						{
							if (task.Exception != null)
							{
								log.WarnException(
									string.Format("Failed to apply conflict on {0} for file '{1}'", destination, FileName),
									task.Exception.ExtractSingleInnerException());
							}

							return new SynchronizationReport()
							{
								FileName = FileName,
								Exception = new SynchronizationException(string.Format("File {0} is conflicted", FileName)),
								Type = SynchronizationType.MetadataUpdate
							};
						});
				}

			    return StartSyncingMedatataTo(destination);
			}).Unwrap()
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
											Type = SynchronizationType.MetadataUpdate
										};

									log.WarnException(string.Format("Metadata synchronization of a file '{0}' to {1} has finished with an exception",
																		FileName, destination), report.Exception);
								}
								else
								{
									report = task.Result;
								}

			              		return report;
			              	});
		}

		private Task<SynchronizationReport> StartSyncingMedatataTo(string destination)
		{
			var request = (HttpWebRequest)WebRequest.Create(destination + "/synchronization/updatemetadata/" + FileName);

			request.Method = "POST";
			request.ContentLength = 0;
			request.AddHeaders(sourceMetadata);

			request.Headers[SyncingMultipartConstants.SourceServerUrl] = SourceServerUrl;

			return request
				.GetResponseAsync()
				.ContinueWith(task =>
				{

				    using (var stream = task.Result.GetResponseStream())
				    {
				        return
				            new JsonSerializer().Deserialize<SynchronizationReport>(
				              	new JsonTextReader(new StreamReader(stream)));
				    }
				}).TryThrowBetterError();
		}
	}
}