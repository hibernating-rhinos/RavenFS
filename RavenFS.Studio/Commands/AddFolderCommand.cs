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

namespace RavenFS.Studio.Commands
{
    public class AddFolderCommand : Command
    {
        private const string FolderNameRegEx = @"^[\w|-]*$";
        private readonly Observable<string> currentFolder;

        public AddFolderCommand(Observable<string> currentFolder)
        {
            this.currentFolder = currentFolder;
        }

        public override void Execute(object parameter)
        {
            var folder = currentFolder.Value;

            AskUser.QuestionAsync("Create Folder", "Folder Name:", ValidateFolderName)
                .ContinueOnUIThread(t =>
                                        {
                                            if (!t.IsCanceled)
                                            {
                                                string path = folder + (folder.EndsWith("/") ? "" : "/") + t.Result;
                                                ApplicationModel.Current.VirtualFolders.Add(path);
                                            }
                                        });
        }

        private string ValidateFolderName(string folderName)
        {
            if (string.IsNullOrWhiteSpace(folderName))
            {
                return "You must enter a name";
            }
            else if (!Regex.IsMatch(folderName, FolderNameRegEx))
            {
                return "Folder name must consist of letters, digits, underscores or dashes";
            }
            else
            {
                return "";
            }
        }
    }
}
