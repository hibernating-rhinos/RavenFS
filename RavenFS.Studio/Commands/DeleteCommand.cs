using System;
using System.Windows;
using System.Windows.Input;
using RavenFS.Client;
using RavenFS.Studio.Infrastructure.Input;
using RavenFS.Studio.Infrastructure;
using RavenFS.Studio.Models;

namespace RavenFS.Studio.Commands
{
	public class DeleteCommand: VirtualItemCommand<FileInfo>
	{
		public DeleteCommand(Observable<VirtualItem<FileInfo>> observableFileInfo) : base(observableFileInfo)
		{
		}

        protected override void ExecuteOverride(FileInfo parameter)
		{
			AskUser.ConfirmationAsync("Delete", string.Format("Are you sure you want to delete file '{0}'?", parameter.Name))
                .ContinueWhenTrueInTheUIThread(
				() => ApplicationModel.Current.AsyncOperations.Do(
				    () => ApplicationModel.Current.Client.DeleteAsync(parameter.Name),
				    "Deleting " + parameter.Name));	
		}
	}
}
