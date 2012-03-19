using System.Windows;
using RavenFS.Studio.Infrastructure;
using RavenFS.Studio.Models;
using RavenFS.Studio.Views;
using FileInfo = RavenFS.Client.FileInfo;

namespace RavenFS.Studio.Commands
{
    public class EditFilePropertiesCommand : VirtualItemCommand<FileSystemModel>
	{
        public EditFilePropertiesCommand(Observable<VirtualItem<FileSystemModel>> observableItem)
            : base(observableItem)
		{
		}

        protected override bool CanExecuteOverride(FileSystemModel item)
        {
            return item is FileModel;
        }

        protected override void ExecuteOverride(FileSystemModel item)
        {
            var model = new FilePropertiesDialogModel { File = item as FileModel };
            var view = new FilePropertiesDialog { Model = model };
            view.Show();
        }

	}
}
