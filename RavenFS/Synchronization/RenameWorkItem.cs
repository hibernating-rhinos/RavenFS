namespace RavenFS.Synchronization
{
	using System;
	using System.IO;
	using System.Net;
	using System.Threading.Tasks;
	using Client;
	using Extensions;
	using Multipart;
	using Newtonsoft.Json;
	using NLog;
	using Storage;

	public class RenameWorkItem : SynchronizationWorkItem
	{
		private readonly Logger log = LogManager.GetCurrentClassLogger();

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

		public async override Task<SynchronizationReport> Perform(string destination)
		{
			SynchronizationReport report;
			try
			{
				report = await StartSyncingRenamingTo(destination);
			}
			catch (Exception ex)
			{
				report = new SynchronizationReport
					         {
						         FileName = FileName,
						         Exception = ex is AggregateException ? ((AggregateException) ex).ExtractSingleInnerException() : ex,
						         Type = SynchronizationType.Rename
					         };
			}

			if (report.Exception == null)
			{
				log.Debug(
					"Synchronization of a renaming of a file '{0}' to '{1}' to destination {2} has finished", FileName, rename, destination);
			}
			else
			{
				log.WarnException(
					string.Format(
						"Synchronization of a renaming of a file '{0}' to '{1}' to destination {2} has finished with an exception",
						FileName, rename, destination), report.Exception);
			}

			return report;
		}

		private async Task<SynchronizationReport> StartSyncingRenamingTo(string destination)
		{
			log.Debug("Synchronizing a renaming of a file '{0}' to '{1}' to {2}", FileName, rename, destination);

			FileAndPages fileAndPages = null;
			Storage.Batch(accessor => fileAndPages = accessor.GetFile(FileName, 0, 0));

			var request = (HttpWebRequest)WebRequest.Create(destination + "/synchronization/rename/" + FileName + "?rename=" + rename);

			request.Method = "PATCH";
			request.ContentLength = 0;
			request.AddHeaders(fileAndPages.Metadata);

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
	}
}