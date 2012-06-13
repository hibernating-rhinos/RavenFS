namespace RavenFS.Synchronization
{
	using System;
	using System.IO;
	using System.Net;
	using System.Threading.Tasks;
	using Multipart;
	using RavenFS.Client;
	using RavenFS.Extensions;
	using Newtonsoft.Json;
	using RavenFS.Storage;

	public class DeleteWorkItem : SynchronizationWorkItem
	{
		private readonly TransactionalStorage storage;

		public DeleteWorkItem(string fileName, string sourceServerUrl, TransactionalStorage storage) : base(fileName, sourceServerUrl)
		{
			this.storage = storage;
		}

		public override Task<SynchronizationReport> Perform(string destination)
		{
			FileAndPages fileAndPages = null;
			storage.Batch(accessor => fileAndPages = accessor.GetFile(FileName, 0, 0));

			var request = (HttpWebRequest)WebRequest.Create(destination + "/synchronization/delete/" + FileName);

			request.Method = "DELETE";
			request.ContentLength = 0;
			request.AddHeaders(fileAndPages.Metadata);

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
						return new SynchronizationReport
						       	{
									FileName = FileName,
						       		Exception = e.ExtractSingleInnerException(),
									Type = SynchronizationType.Deletion
						       	};
					}
				});
		}
	}
}