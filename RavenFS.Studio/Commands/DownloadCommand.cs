using System;
using System.Windows;
using System.Windows.Browser;
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
			var url = ApplicationModel.GetFileUrl(FileName);
			HtmlPage.Window.Navigate(url);
		}
	}
}
