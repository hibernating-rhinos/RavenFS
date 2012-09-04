namespace RavenFS.Synchronization
{
	using System;
	using System.Collections.Specialized;
	using System.IO;
	using System.Net;
	using System.Threading.Tasks;
	using Extensions;
	using Multipart;
	using Newtonsoft.Json;
	using NLog;
	using RavenFS.Client;
	using Storage;

	public class MetadataUpdateWorkItem : SynchronizationWorkItem
	{
		private readonly Logger log = LogManager.GetCurrentClassLogger();

		private readonly NameValueCollection destinationMetadata;

		public MetadataUpdateWorkItem(string fileName, NameValueCollection destinationMetadata, TransactionalStorage storage)
			: base(fileName, storage)
		{
			this.destinationMetadata = destinationMetadata;
		}

		public override SynchronizationType SynchronizationType
		{
			get { return SynchronizationType.MetadataUpdate; }
		}

		public async override Task<SynchronizationReport> Perform(string destination)
		{
			SynchronizationReport report;
			try
			{
				AssertLocalFileExistsAndIsNotConflicted(FileMetadata);
				report = await StartSyncingMedatataTo(destination);
			}
			catch (Exception ex)
			{
				report = new SynchronizationReport
										{
											FileName = FileName,
											Exception = ex is AggregateException ? ((AggregateException)ex).ExtractSingleInnerException() : ex,
											Type = SynchronizationType.MetadataUpdate
										};
			}

			if (report.Exception == null)
			{
				log.Debug("Metadata synchronization of a file '{0}' to {1} has succeeded", FileName,
						  destination);
			}
			else
			{
				log.WarnException(
					string.Format("Metadata synchronization of a file '{0}' to {1} has finished with an exception",
								  FileName, destination), report.Exception);
			}

			return report;
		}

		private async Task<SynchronizationReport> StartSyncingMedatataTo(string destination)
		{
			log.Debug("Synchronizing a metadata of a file '{0}' to {1}", FileName, destination);

			var conflict = CheckConflictWithDestination(FileMetadata, destinationMetadata);

			if (conflict != null)
			{
				return await ApplyConflictOnDestination(conflict, destination, log);
			}

			var request = (HttpWebRequest)WebRequest.Create(destination + "/synchronization/updatemetadata/" + FileName);

			request.Method = "POST";
			request.ContentLength = 0;
			request.AddHeaders(FileMetadata);

			request.Headers[SyncingMultipartConstants.SourceServerId] = SourceServerId.ToString();

			try
			{
				using (var response = await request.GetResponseAsync())
				{
					using (var stream = response.GetResponseStream())
					{
						return new JsonSerializer().Deserialize<SynchronizationReport>(new JsonTextReader(new StreamReader(stream)));
					}
				}
			}
			catch (WebException exception)
			{
				throw exception.BetterWebExceptionError();
			}
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
			return Equals(other.FileName, FileName) && Equals(other.FileETag, FileETag);
		}

		public override int GetHashCode()
		{
			return (FileName != null ? GetType().Name.GetHashCode() ^ FileName.GetHashCode() ^ FileETag.GetHashCode() : 0);
		}
	}
}