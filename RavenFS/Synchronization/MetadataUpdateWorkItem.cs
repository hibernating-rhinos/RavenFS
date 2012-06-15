namespace RavenFS.Synchronization
{
	using System.Collections.Specialized;
	using System.IO;
	using System.Net;
	using System.Threading.Tasks;
	using Multipart;
	using Newtonsoft.Json;
	using RavenFS.Client;
	using RavenFS.Extensions;

	public class MetadataUpdateWorkItem : SynchronizationWorkItem
	{
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

				var conflict = CheckConflictWithDestination(sourceMetadata, destinationMetadata);

				if (conflict != null)
				{
					return ApplyConflictOnDestination(conflict, destination);
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