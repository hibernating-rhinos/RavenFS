namespace RavenFS.Synchronization
{
	using System;
	using System.IO;
	using System.Net;
	using System.Threading.Tasks;
	using Multipart;
	using Newtonsoft.Json;
	using RavenFS.Client;
	using RavenFS.Extensions;
	using RavenFS.Storage;

	public class DeleteWorkItem : SynchronizationWorkItem
	{
		public DeleteWorkItem(string fileName, string sourceServerUrl, TransactionalStorage storage)
			: base(fileName, sourceServerUrl, storage)
		{
		}

		public override SynchronizationType SynchronizationType
		{
			get { return SynchronizationType.Delete; }
		}

		public async override Task<SynchronizationReport> PerformAsync(string destination)
		{
			FileAndPages fileAndPages = null;
			Storage.Batch(accessor => fileAndPages = accessor.GetFile(FileName, 0, 0));

			var request = (HttpWebRequest)WebRequest.Create(destination + "/synchronization/delete?fileName=" + Uri.EscapeDataString(FileName));

			request.Method = "DELETE";
			request.ContentLength = 0;
			request.AddHeaders(fileAndPages.Metadata);

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
			if (obj.GetType() != typeof(DeleteWorkItem)) return false;
			return Equals((DeleteWorkItem)obj);
		}

		public bool Equals(DeleteWorkItem other)
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
			return string.Format("Synchronization of a deleted file '{0}'", FileName);
		}
	}
}