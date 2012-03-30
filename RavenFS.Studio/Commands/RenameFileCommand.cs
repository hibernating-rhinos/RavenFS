using System;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using RavenFS.Studio.Infrastructure;
using RavenFS.Studio.Infrastructure.Input;
using RavenFS.Studio.Models;
using RavenFS.Studio.Views;
using RavenFS.Studio.Extensions;

namespace RavenFS.Studio.Commands
{
    public class RenameFileCommand : VirtualItemCommand<FileSystemModel>
    {
        private const string FileNameRegEx = @"^[\w|\-\.\s]*$";

        public RenameFileCommand(Observable<VirtualItem<FileSystemModel>> observableItem)
            : base(observableItem)
        {
        }

        protected override bool CanExecuteOverride(FileSystemModel item)
        {
            return item is FileModel;
        }

        protected override void ExecuteOverride(FileSystemModel item)
        {
            var folder = GetFolderName(item.FullPath);
            var fileName = GetFileName(item.FullPath);

            AskUser.QuestionAsync(string.Format("Rename File '{0}'", fileName), "New Name:", ValidateFileName, defaultAnswer:fileName)
                .ContinueOnUIThread(t =>
                {
                    if (!t.IsCanceled)
                    {
                        string newName = t.Result;
                        if (newName == fileName)
                        {
                            return;
                        }

                        var newFullPath = folder + (folder.IsNullOrEmpty() ? "" : "/") + newName;
                        ApplicationModel.Current.AsyncOperations.Do(
                            () => ApplicationModel.Current.Client.RenameAsync(item.FullPath, newFullPath), 
                            string.Format("Renaming '{0}' to '{1}'", fileName, newName));
                    }
                });
        }

        private string GetFolderName(string path)
        {
            var lastSlash = path.LastIndexOf("/", StringComparison.InvariantCulture);
            if (lastSlash <= 0)
            {
                return "";
            }
            else
            {
                return path.Substring(0, lastSlash);
            }
        }

        private string GetFileName(string path)
        {
            var lastSlash = path.LastIndexOf("/", StringComparison.InvariantCulture);
            if (lastSlash < 0)
            {
                return path;
            }
            else
            {
                return path.Substring(lastSlash + 1);
            }
        }

        private string ValidateFileName(string folderName)
        {
            if (string.IsNullOrWhiteSpace(folderName))
            {
                return "You must enter a name";
            }
            else if (!Regex.IsMatch(folderName, FileNameRegEx))
            {
                return "File name must consist of letters, digits, dots, underscores or dashes";
            }
            else
            {
                return "";
            }
        }
    }
}
