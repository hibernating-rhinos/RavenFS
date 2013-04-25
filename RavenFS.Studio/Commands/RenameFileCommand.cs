using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RavenFS.Studio.Infrastructure;
using RavenFS.Studio.Infrastructure.Input;
using RavenFS.Studio.Models;
using RavenFS.Studio.Extensions;

namespace RavenFS.Studio.Commands
{
    public class RenameFileCommand : VirtualItemSelectionCommand<FileSystemModel>
    {
        private const string FileNameRegEx = @"^[\w|\-\.\s]*$";

        public RenameFileCommand(ItemSelection<VirtualItem<FileSystemModel>> itemSelection)
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

            var folder = GetFolderName(item.FullPath);
            var fileName = GetFileName(item.FullPath);

            AskUser.QuestionAsync(string.Format("Rename File '{0}'", fileName), "New Name:", ValidateFileName, defaultAnswer:fileName)
                .ContinueOnUIThread(t =>
                {
                    if (!t.IsCanceled)
                    {
                        var newName = t.Result;
	                    if (newName == fileName)
		                    return;

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
	        return lastSlash > 0 ? path.Substring(0, lastSlash) : "";
        }

        private string GetFileName(string path)
        {
	        var lastSlash = path.LastIndexOf("/", StringComparison.InvariantCulture);
	        return lastSlash < 0 ? path : path.Substring(lastSlash + 1);
        }

	    private string ValidateFileName(string folderName)
	    {
		    if (string.IsNullOrWhiteSpace(folderName))
			    return "You must enter a name";

		    if (!Regex.IsMatch(folderName, FileNameRegEx))
			    return "File name must consist of letters, digits, dots, underscores or dashes";
		    
			return "";
	    }
    }
}
