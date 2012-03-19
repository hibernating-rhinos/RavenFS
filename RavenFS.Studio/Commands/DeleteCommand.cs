using System;
using System.Windows;
using System.Windows.Input;
using RavenFS.Client;
using RavenFS.Studio.Infrastructure.Input;
using RavenFS.Studio.Infrastructure;
using RavenFS.Studio.Models;

namespace RavenFS.Studio.Commands
{
    public class DeleteCommand : VirtualItemCommand<FileSystemModel>
	{
        public DeleteCommand(Observable<VirtualItem<FileSystemModel>> observableFileInfo)
            : base(observableFileInfo)
		{
		}

        protected override bool CanExecuteOverride(FileSystemModel item)
        {
            return item is FileModel;
        }

        protected override void ExecuteOverride(FileSystemModel parameter)
		{
			AskUser.ConfirmationAsync("Delete", string.Format("Are you sure you want to delete file '{0}'?", parameter.Name))
                .ContinueWhenTrueInTheUIThread(
				() => ApplicationModel.Current.AsyncOperations.Do(
				    () => ApplicationModel.Current.Client.DeleteAsync(parameter.FullPath),
				    "Deleting " + parameter.Name));	
		}
	}
}
