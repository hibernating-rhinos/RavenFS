using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

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
