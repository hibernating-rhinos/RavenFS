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
    public class NavigateToFileSystemModelCommand : Command
    {
        public override bool CanExecute(object parameter)
        {
            var item = parameter as VirtualItem<FileSystemModel>;
            return item != null && item.IsRealized && !item.IsStale;
        }

        public override void Execute(object parameter)
        {
            var model = (parameter as VirtualItem<FileSystemModel>).Item;

            if (model is FileModel)
            {
                Navigation.Download(model.FullPath);
            }
            else if (model is DirectoryModel)
            {
                Navigation.Folder(model.FullPath);
            }
        }
    }
}
