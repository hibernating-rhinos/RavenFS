using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RavenFS.Client;
using RavenFS.Studio.Infrastructure;

namespace RavenFS.Studio.Commands
{
	public class BrowseCommand : ICommand
	{
		public bool CanExecute(object parameter)
		{
			return true;
		}

		public void Execute(object parameter)
		{
			var fileDialog = new OpenFileDialog();
			var result = fileDialog.ShowDialog();
			if (result != true)
				return;

			var stream = fileDialog.File.OpenRead();
			var length = stream.Length;
			ApplicationModel.Client.UploadAsync(fileDialog.File.Name, new NameValueCollection(), stream, Progress)
				.ContinueWith(task =>
				{
					stream.Dispose();
					task.Wait();
					return task;
				});

			Application.Current.Host.NavigationState += "?" + Guid.NewGuid();
		}

		private void Progress(string file, int uploaded)
		{
			
		}

		public event EventHandler CanExecuteChanged = delegate { };
	}
}
