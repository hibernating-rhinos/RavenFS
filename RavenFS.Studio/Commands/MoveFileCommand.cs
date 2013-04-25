using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RavenFS.Studio.Infrastructure;
using RavenFS.Studio.Infrastructure.Input;
using RavenFS.Studio.Models;

namespace RavenFS.Studio.Commands
{
    public class MoveFileCommand : VirtualItemSelectionCommand<FileSystemModel>
    {
        private const string FolderPathRegEx = @"^(/[\w|\-\.\s]*)+$";

        public MoveFileCommand(ItemSelection<VirtualItem<FileSystemModel>> itemSelection)
            : base(itemSelection)
        {
        }

        protected override bool CanExecuteOverride(IList<FileSystemModel> items)
        {
            return items.Any(i => i is FileModel);
        }

        protected override void ExecuteOverride(IList<FileSystemModel> items)
        {
            var firstItem = items.First();

            var originalFolderPath = GetFolderName(firstItem.FullPath);
            var firstItemFileName = GetFileName(firstItem.FullPath);

            originalFolderPath = "/" + originalFolderPath;

            var title = items.Count == 1 ? string.Format("Move File '{0}'", firstItemFileName)
                : string.Format("Move {0} Files", items.Count);

            AskUser.QuestionAsync(title, "New Folder Path:", ValidateFolderPath, defaultAnswer: originalFolderPath)
                .ContinueOnUIThread(t =>
                {
                    if (!t.IsCanceled)
                    {
                        string newPath = t.Result;
	                    if (newPath == originalFolderPath)
		                    return;

	                    if (!newPath.EndsWith("/"))
                        {
                            newPath += "/";
                        }

                        foreach (var item in items)
                        {
                            var originalFullPath = item.FullPath;
                            var fileName = GetFileName(item.FullPath);

                            var newFullPath = newPath + fileName;
                            newFullPath = newFullPath.TrimStart('/');

                            ApplicationModel.Current.AsyncOperations.Do(
                                () => ApplicationModel.Current.Client.RenameAsync(originalFullPath, newFullPath), 
                                string.Format("Moving '{0}' to '{1}'", fileName, newPath));
                        }

                    }
                });
        }

        private string GetFolderName(string path)
        {
	        var lastSlash = path.LastIndexOf("/", StringComparison.InvariantCulture);
	        return lastSlash <= 0 ? "" : path.Substring(0, lastSlash);
        }

	    private string GetFileName(string path)
	    {
		    var lastSlash = path.LastIndexOf("/", StringComparison.InvariantCulture);
		    return lastSlash < 0 ? path : path.Substring(lastSlash + 1);
	    }

	    private string ValidateFolderPath(string folderPath)
	    {
		    if (string.IsNullOrWhiteSpace(folderPath))
			    return "You must enter a path";

		    if (!folderPath.StartsWith("/"))
			    return "The path must start with /";

		    if (!Regex.IsMatch(folderPath, FolderPathRegEx))
			    return "Folder path contains invalid characters";
		    
			return "";
	    }
    }
}