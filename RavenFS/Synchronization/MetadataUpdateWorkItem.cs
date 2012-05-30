namespace RavenFS.Synchronization
{
	using System;
	using System.Collections.Specialized;
	using System.IO;
	using System.Net;
	using System.Threading.Tasks;
	using Multipart;
	using RavenFS.Client;
	using RavenFS.Extensions;
	using Newtonsoft.Json;

	public class MetadataUpdateWorkItem : SynchronizationWorkItem
	{
		private readonly NameValueCollection metadata;

		public MetadataUpdateWorkItem(string fileName, NameValueCollection metadata, string sourceUrl) : base(fileName, sourceUrl)
		{
			this.metadata = metadata;
		}

		public override Task<SynchronizationReport> Perform(string destination)
		{
			var request = (HttpWebRequest)WebRequest.Create(destination + "/synchronization/updatemetadata/" + FileName);

			request.Method = "POST";
			request.ContentLength = 0;
			request.AddHeaders(metadata);

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
							return new SynchronizationReport { Exception = e.ExtractSingleInnerException() };
						}
					});
		}
	}
}