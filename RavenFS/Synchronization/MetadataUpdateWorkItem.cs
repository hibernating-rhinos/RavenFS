namespace RavenFS.Synchronization
{
	using System.Collections.Specialized;
	using System.IO;
	using System.Net;
	using System.Threading.Tasks;
	using Multipart;
	using Newtonsoft.Json;
	using NLog;
	using RavenFS.Client;
	using RavenFS.Extensions;

	public class MetadataUpdateWorkItem : SynchronizationWorkItem
	{
		private readonly Logger log = LogManager.GetCurrentClassLogger();

		private readonly NameValueCollection sourceMetadata;
		private readonly NameValueCollection destinationMetadata;

		public MetadataUpdateWorkItem(string fileName, NameValueCollection sourceMetadata, NameValueCollection destinationMetadata, string sourceUrl)
			: base(fileName, sourceUrl)
		{
			this.sourceMetadata = sourceMetadata;
			this.destinationMetadata = destinationMetadata;
		}

		public override SynchronizationType SynchronizationType
		{
			get { return SynchronizationType.MetadataUpdate; }
		}

		public override Task<SynchronizationReport> Perform(string destination)
		{
			try
			{
				AssertLocalFileExistsAndIsNotConflicted(sourceMetadata);
			}
			catch (SynchronizationException ex)
			{
				log.WarnException(
					string.Format("Failed to perform a metadata synchronization of a file '{0}' to {1} has finished with an exception",
					              FileName, destination), ex);

				return SynchronizationUtils.SynchronizationExceptionReport(FileName, ex.Message);
			}
			
			var conflict = CheckConflictWithDestination(sourceMetadata, destinationMetadata);

			if (conflict != null)
			{
				return ApplyConflictOnDestination(conflict, destination, log);
			}

			return StartSyncingMedatataTo(destination)
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

									log.WarnException(
										string.Format(
											"Failed to perform a metadata synchronization of a file '{0}' to {1} has finished with an exception",
											FileName, destination), report.Exception);
								}
								else
								{
									report = task.Result;

									if (report.Exception == null)
									{
										log.Debug("Metadata synchronization of a file '{0}' to {1} has finished with an exception", FileName,
										          destination);
									}
									else
									{
										log.WarnException(
											string.Format("Metadata synchronization of a file '{0}' to {1} has finished with an exception",
											              FileName, destination), report.Exception);
									}
								}

			              		return report;
			              	});
		}

		private Task<SynchronizationReport> StartSyncingMedatataTo(string destination)
		{
			log.Debug("Synchronizing a metadata of a file '{0}' to {1}", FileName, destination);

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

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != typeof(MetadataUpdateWorkItem)) return false;
			return Equals((MetadataUpdateWorkItem)obj);
		}

		public bool Equals(MetadataUpdateWorkItem other)
		{
			if (ReferenceEquals(null, other)) return false;
			if (ReferenceEquals(this, other)) return true;
			return Equals(other.FileName, FileName);
		}

		public override int GetHashCode()
		{
			return (FileName != null ? GetType().Name.GetHashCode() ^ FileName.GetHashCode() : 0);
		}
	}
}