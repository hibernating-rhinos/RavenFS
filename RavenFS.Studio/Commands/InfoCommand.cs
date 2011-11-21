using System;
using System.Windows;
using System.Windows.Input;

namespace RavenFS.Studio.Commands
{
	public class InfoCommand:ICommand
	{
		private readonly string fileName;

		public InfoCommand(string fileName)
		{
			this.fileName = fileName;
		}

		public bool CanExecute(object parameter)
		{
			return true;
		}

		public void Execute(object parameter)
		{
			Application.Current.Host.NavigationState = "/fileInfo?name=" + fileName;
		}

		public event EventHandler CanExecuteChanged;
	}
}
