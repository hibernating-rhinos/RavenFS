using System.Collections.Generic;
using System.Linq;
using RavenFS.Studio.Infrastructure;
using RavenFS.Studio.Models;

namespace RavenFS.Studio.Commands
{
    public class NavigateToParentFolderCommand : VirtualItemSelectionCommand<FileSystemModel>
    {
        public NavigateToParentFolderCommand(ItemSelection<VirtualItem<FileSystemModel>> itemSelection) : base(itemSelection)
        {
        }

        protected override bool CanExecuteOverride(IList<FileSystemModel> items)
        {
            return items.Count == 1 && items.First() is FileModel;
        }

        protected override void ExecuteOverride(IList<FileSystemModel> items)
        {
            Navigation.Folder(items.First().Folder);
        }
    }
}
