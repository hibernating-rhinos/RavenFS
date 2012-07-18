using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using RavenFS.Client;
using RavenFS.Studio.Infrastructure.Input;
using RavenFS.Studio.Infrastructure;
using RavenFS.Studio.Models;

namespace RavenFS.Studio.Commands
{
    public class DeleteCommand : VirtualItemSelectionCommand<FileSystemModel>
	{
        public DeleteCommand(ItemSelection<VirtualItem<FileSystemModel>> itemSelection)
            : base(itemSelection)
		{
		}

        protected override bool CanExecuteOverride(IList<FileSystemModel> items)
        {
            return items.Any(i => i is FileModel);
        }

        protected override void ExecuteOverride(IList<FileSystemModel> items)
        {
            var message = items.Count == 1 ? string.Format("Are you sure you want to delete file '{0}'?", items[0].Name)
                : string.Format("Are you sure you want to delete {0} selected files?", items.Count);

            AskUser.ConfirmationAsync("Delete", message)
                .ContinueWhenTrueInTheUIThread(
				() =>
				    {
				        foreach (var item in items)
				        {
				            var capturedItem = item;
				            ApplicationModel.Current.AsyncOperations.Do(
				                () => ApplicationModel.Current.Client.DeleteAsync(capturedItem.FullPath),
				                "Deleting " + capturedItem.Name);
				        }

				    });
        }
	}
}
