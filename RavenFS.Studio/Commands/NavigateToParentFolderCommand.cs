using System;
using System.Net;
using System.Windows;
using System.Windows.Browser;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using RavenFS.Studio.Infrastructure;
using RavenFS.Studio.Models;

namespace RavenFS.Studio.Commands
{
    public class NavigateToParentFolderCommand : VirtualItemCommand<FileSystemModel>
    {
        public NavigateToParentFolderCommand(Observable<VirtualItem<FileSystemModel>> observableItem) : base(observableItem)
        {
        }

        protected override bool CanExecuteOverride(FileSystemModel item)
        {
            return item is FileModel;
        }

        protected override void ExecuteOverride(FileSystemModel item)
        {
            Navigation.Folder(item.Folder);
        }
    }
}
