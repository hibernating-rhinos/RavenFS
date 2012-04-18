using System;
using System.Collections.Generic;
using System.Linq;
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
