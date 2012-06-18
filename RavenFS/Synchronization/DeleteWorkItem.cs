namespace RavenFS.Synchronization
{
	using System;
	using System.IO;
	using System.Net;
	using System.Threading.Tasks;
	using Multipart;
	using NLog;
	using RavenFS.Client;
	using RavenFS.Extensions;
	using Newtonsoft.Json;
	using RavenFS.Storage;

	public class DeleteWorkItem : SynchronizationWorkItem
	{
		private readonly Logger log = LogManager.GetCurrentClassLogger();

		private readonly TransactionalStorage storage;

		public DeleteWorkItem(string fileName, string sourceServerUrl, TransactionalStorage storage) : base(fileName, sourceServerUrl)
		{
			this.storage = storage;
		}

		public override Task<SynchronizationReport> Perform(string destination)
		{
			return StartDeleteFileOn(destination)
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
								Type = SynchronizationType.Renaming
							};

						log.WarnException(
							string.Format(
								"Failed to perform a synchronization of a deleted file file '{0}' on destination {1}. It has finished with an exception",
								FileName, destination), report.Exception);
					}
					else
					{
						report = task.Result;

						if (report.Exception == null)
						{
							log.Debug(
								"Synchronization of a deleted file '{0}' on destination {1} has finished", FileName, destination);
						}
						else
						{
							log.WarnException(
								string.Format(
									"Synchronization of a deleted file '{0}' on destination {1} has finished with an exception",
									FileName, destination), report.Exception);
						}
					}

					return report;
				});
		}

		private Task<SynchronizationReport> StartDeleteFileOn(string destination)
		{
			log.Debug("Synchronizing a deletion of a file '{0}' to {1}", FileName, destination);

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
					using (var stream = task.Result.GetResponseStream())
					{
						return new JsonSerializer().Deserialize<SynchronizationReport>(new JsonTextReader(new StreamReader(stream)));
					}
				})
				.TryThrowBetterError();
		}
	}
}