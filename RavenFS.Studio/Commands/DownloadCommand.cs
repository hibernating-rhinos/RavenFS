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
    public class DownloadCommand : VirtualItemCommand<FileSystemModel>
	{
        public DownloadCommand(Observable<VirtualItem<FileSystemModel>> observableFileInfo)
            : base(observableFileInfo) 
		{	
		}

        protected override bool CanExecuteOverride(FileSystemModel item)
        {
            return item is FileModel;
        }

        protected override void ExecuteOverride(FileSystemModel item)
        {
            var url = ApplicationModel.Current.GetFileUrl(item.Name);
            HtmlPage.Window.Navigate(url);
        }
	}
}
