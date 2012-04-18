using System.Collections.Generic;
using System.Linq;
using System.Windows;
using RavenFS.Studio.Infrastructure;
using RavenFS.Studio.Models;
using RavenFS.Studio.Views;
using FileInfo = RavenFS.Client.FileInfo;

namespace RavenFS.Studio.Commands
{
    public class EditFilePropertiesCommand : VirtualItemSelectionCommand<FileSystemModel>
	{
        public EditFilePropertiesCommand(ItemSelection<VirtualItem<FileSystemModel>> itemSelection)
            : base(itemSelection)
		{
		}

        protected override bool CanExecuteOverride(IList<FileSystemModel> items)
        {
            return items.Count == 1 && items.First() is FileModel;
        }

        protected override void ExecuteOverride(IList<FileSystemModel> items)
        {
            var item = items.First();

            var model = new FilePropertiesDialogModel { File = item as FileModel };
            var view = new FilePropertiesDialog { Model = model };
            view.Show();
        }

	}
}
