using System.Text.RegularExpressions;
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
                                                var path = folder + (folder.EndsWith("/") ? "" : "/") + t.Result;
                                                ApplicationModel.Current.State.VirtualFolders.Add(path);
                                            }
                                        });
        }

        private string ValidateFolderName(string folderName)
        {
	        if (string.IsNullOrWhiteSpace(folderName))
		        return "You must enter a name";

	        return !Regex.IsMatch(folderName, FolderNameRegEx) ? "Folder name must consist of letters, digits, underscores or dashes" : "";
        }
    }
}