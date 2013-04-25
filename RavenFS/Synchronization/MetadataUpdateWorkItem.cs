using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using NLog;
using Newtonsoft.Json;
using RavenFS.Client;
using RavenFS.Extensions;
using RavenFS.Storage;
using RavenFS.Synchronization.Multipart;

namespace RavenFS.Synchronization
{
	public class MetadataUpdateWorkItem : SynchronizationWorkItem
	{
		private readonly NameValueCollection destinationMetadata;
		private readonly Logger log = LogManager.GetCurrentClassLogger();

		public MetadataUpdateWorkItem(string fileName, string sourceServerUrl, NameValueCollection destinationMetadata,
		                              TransactionalStorage storage)
			: base(fileName, sourceServerUrl, storage)
		{
			this.destinationMetadata = destinationMetadata;
		}

		public override SynchronizationType SynchronizationType
		{
			get { return SynchronizationType.MetadataUpdate; }
		}

		public override async Task<SynchronizationReport> PerformAsync(string destination)
		{
			AssertLocalFileExistsAndIsNotConflicted(FileMetadata);

			var conflict = CheckConflictWithDestination(FileMetadata, destinationMetadata, ServerInfo.Url);

			if (conflict != null)
				return await ApplyConflictOnDestinationAsync(conflict, destination, ServerInfo.Url, log);

			var request =
				(HttpWebRequest)
				WebRequest.Create(destination + "/synchronization/updatemetadata?fileName=" + Uri.EscapeDataString(FileName));

			request.Method = "POST";
			request.ContentLength = 0;
			request.AddHeaders(FileMetadata);

			request.Headers[SyncingMultipartConstants.SourceServerInfo] = ServerInfo.AsJson();

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
			if (obj.GetType() != typeof (MetadataUpdateWorkItem)) return false;
			return Equals((MetadataUpdateWorkItem) obj);
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

		public override string ToString()
		{
			return string.Format("Metadata synchronization of a file '{0}'", FileName);
		}
	}
}