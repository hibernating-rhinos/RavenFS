using System.Windows;
using RavenFS.Studio.Infrastructure;
using RavenFS.Studio.Models;
using RavenFS.Studio.Views;
using FileInfo = RavenFS.Client.FileInfo;

namespace RavenFS.Studio.Commands
{
	public class EditFilePropertiesCommand : VirtualItemCommand<FileInfo>
	{
        public EditFilePropertiesCommand(Observable<VirtualItem<FileInfo>> observableItem) :base(observableItem)
		{
		}

        protected override void ExecuteOverride(FileInfo item)
        {
            var model = new FilePropertiesDialogModel { Name = item.Name };
            var view = new FilePropertiesDialog { Model = model };
            view.Show();
        }

	}
}
