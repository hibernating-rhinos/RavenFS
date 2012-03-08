using System;
using System.Windows;
using System.Windows.Browser;
using System.Windows.Controls;
using System.Windows.Input;
using RavenFS.Client;
using RavenFS.Studio.Infrastructure;
using RavenFS.Studio.Models;

namespace RavenFS.Studio.Commands
{
	public class DownloadCommand : VirtualItemCommand<FileInfo>
	{
		public DownloadCommand(Observable<VirtualItem<FileInfo>> observableFileInfo) : base(observableFileInfo) 
		{	
		}

        protected override void ExecuteOverride(FileInfo item)
        {
            var url = ApplicationModel.Current.GetFileUrl(item.Name);
            HtmlPage.Window.Navigate(url);
        }
	}
}
