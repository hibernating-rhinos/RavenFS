using System.Windows.Controls;
using RavenFS.Client;
using RavenFS.Studio.Infrastructure;

namespace RavenFS.Studio.Commands
{
	public class UploadCommand : Command
	{
		private readonly Observable<long> totalUploadFileSize;
		private readonly Observable<long> totalBytesUploaded;

		public UploadCommand(Observable<long> totalUploadFileSize, Observable<long> totalBytesUploaded)
		{
			this.totalUploadFileSize = totalUploadFileSize;
			this.totalBytesUploaded = totalBytesUploaded;
		}

		public override void Execute(object parameter)
		{
			var fileDialog = new OpenFileDialog();
			var result = fileDialog.ShowDialog();
			if (result != true)
				return;

			var stream = fileDialog.File.OpenRead();
			totalUploadFileSize.Value = stream.Length;
			ApplicationModel.Client.UploadAsync(fileDialog.File.Name, new NameValueCollection(), stream, Progress)
				.ContinueWith(task =>
				{
					stream.Dispose();
					task.Wait();
					return task;
				});
		}

		private void Progress(string file, int uploaded)
		{
			totalBytesUploaded.Value = uploaded;
		}
	}
}
