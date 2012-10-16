namespace RavenFS.Synchronization
{
	using System.IO;
	using System.Net;
	using System.Threading.Tasks;
	using Client;
	using Extensions;
	using Multipart;
	using Newtonsoft.Json;
	using Storage;

	public class RenameWorkItem : SynchronizationWorkItem
	{
		private readonly string rename;

		public RenameWorkItem(string name, string rename, TransactionalStorage storage)
			: base(name, storage)
		{
			this.rename = rename;
		}

		public override SynchronizationType SynchronizationType
		{
			get { return SynchronizationType.Rename; }
		}

		public async override Task<SynchronizationReport> PerformAsync(string destination, string source)
		{
			FileAndPages fileAndPages = null;
			Storage.Batch(accessor => fileAndPages = accessor.GetFile(FileName, 0, 0));

			var request = (HttpWebRequest)WebRequest.Create(destination + "/synchronization/rename/" + FileName + "?rename=" + rename);

			request.Method = "PATCH";
			request.ContentLength = 0;
			request.AddHeaders(fileAndPages.Metadata);

			request.Headers[SyncingMultipartConstants.SourceServerId] = SourceServerId.ToString();
			request.Headers[SyncingMultipartConstants.SourceServerUrl] = source;

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
			if (obj.GetType() != typeof(RenameWorkItem)) return false;
			return Equals((RenameWorkItem)obj);
		}

		public bool Equals(RenameWorkItem other)
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
			return string.Format("Synchronization of a renaming of a file '{0}' to '{1}'", FileName, rename);
		}
	}
}