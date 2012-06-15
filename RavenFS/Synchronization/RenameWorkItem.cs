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
		private readonly TransactionalStorage storage;

		public RenameWorkItem(string name, string rename, string sourceServerUrl, TransactionalStorage storage)
			: base(name, sourceServerUrl)
		{
			this.rename = rename;
			this.storage = storage;
		}

		public override Task<SynchronizationReport> Perform(string destination)
		{
			return StartSyncingRenamingTo(destination)
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
									"Failed to perform a synchronization of a renaming of a file '{0}' to '{1}' to destination {2} has finished with an exception",
									FileName, rename, destination), report.Exception);
						}
						else
						{
							report = task.Result;

							if (report.Exception == null)
							{
								log.Debug(
									"Synchronization of a renaming of a file '{0}' to '{1}' to destination {2} has finished", FileName, rename,
									destination);
							}
							else
							{
								log.WarnException(
									string.Format(
										"Synchronization of a renaming of a file '{0}' to '{1}' to destination {2} has finished with an exception",
										FileName, rename,
										destination), report.Exception);
							}
						}

						return report;
					});
		}

		private Task<SynchronizationReport> StartSyncingRenamingTo(string destination)
		{
			log.Debug("Synchronizing a renaming of a file '{0}' to '{1}' to {2}", FileName, rename, destination);

			FileAndPages fileAndPages = null;
			storage.Batch(accessor => fileAndPages = accessor.GetFile(FileName, 0, 0));

			var request = (HttpWebRequest)WebRequest.Create(destination + "/synchronization/rename/" + FileName + "?rename=" + rename);

			request.Method = "PATCH";
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