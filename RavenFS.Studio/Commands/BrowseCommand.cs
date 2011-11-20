using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
			ApplicationModel.Client.Upload(fileDialog.File.Name, stream)
				.ContinueWith(task =>
				{
					stream.Dispose();
					task.Wait();
					return task;
				});
		}

		public event EventHandler CanExecuteChanged = delegate { };
	}
}
