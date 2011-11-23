using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RavenFS.Studio.Infrastructure;

namespace RavenFS.Studio.Commands
{
	public class DownloadCommand : Command
	{
		private string FileName;

		public DownloadCommand() : this("")
		{	
		}

		public DownloadCommand(string fileName)
		{
			FileName = fileName;
		}

		public override void Execute(object parameter)
		{
			var item = parameter as FileInfoWrapper;
			if (item != null)
			{
				FileName = item.File.Name;
			}
			var fileDialog = new SaveFileDialog();
			var result = fileDialog.ShowDialog();
			if (result != true)
				return;

			var stream = fileDialog.OpenFile();
			var dispatcher = Deployment.Current.Dispatcher;
			ApplicationModel.Client.DownloadAsync(FileName,stream)
				.ContinueWith(task =>
				{
					dispatcher.InvokeAsync(() =>
					{
						stream.Flush();
						stream.Dispose();
					});
					task.Wait();
					return task;
				});
	
			Application.Current.Host.NavigationState += "?" + Guid.NewGuid();
		}
	}
}
