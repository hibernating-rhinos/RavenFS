namespace RavenFS.Rdc
{
	using System.Collections.Specialized;
	using System.IO;
	using System.Net;
	using System.Threading.Tasks;
	using Client;
	using Extensions;
	using Multipart;
	using Newtonsoft.Json;

	public class MetadataUpdateWorkItem : SynchronizationWorkItem
	{
		private readonly NameValueCollection metadata;
		private readonly string sourceUrl;

		public MetadataUpdateWorkItem(string fileName, NameValueCollection metadata, string sourceUrl) : base(fileName)
		{
			this.metadata = metadata;
			this.sourceUrl = sourceUrl;
		}

		public override Task<SynchronizationReport> Perform(string destination)
		{
			var request = (HttpWebRequest)WebRequest.Create(destination + "/synchronization/updatemetadata/" + FileName);

			request.Method = "POST";
			request.ContentLength = 0;
			request.AddHeaders(metadata);

			request.Headers[SyncingMultipartConstants.SourceServerUrl] = sourceUrl;

			return request
				.GetResponseAsync()
				.ContinueWith(task =>
					{
						using (var stream = task.Result.GetResponseStream())
						{
							return new JsonSerializer().Deserialize<SynchronizationReport>(new JsonTextReader(new StreamReader(stream)));
						}
					})
					.TryThrowBetterError();
		}
	}
}