﻿using System;
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

namespace RavenFS.Studio.Commands
{
    public class MoveFileCommand : VirtualItemCommand<FileSystemModel>
    {
        private const string FolderPathRegEx = @"^(/[\w|\-\.\s]*)+$";

        public MoveFileCommand(Observable<VirtualItem<FileSystemModel>> observableItem)
            : base(observableItem)
        {
        }

        protected override bool CanExecuteOverride(FileSystemModel item)
        {
            return item is FileModel;
        }

        protected override void ExecuteOverride(FileSystemModel item)
        {
            var originalFolderPath = GetFolderName(item.FullPath);
            var fileName = GetFileName(item.FullPath);

            originalFolderPath = "/" + originalFolderPath;

            AskUser.QuestionAsync(string.Format("Move File '{0}'", fileName), "New Folder Path:", ValidateFolderPath, defaultAnswer: originalFolderPath)
                .ContinueOnUIThread(t =>
                {
                    if (!t.IsCanceled)
                    {
                        string newPath = t.Result;
                        if (newPath == originalFolderPath)
                        {
                            return;
                        }

                        var newFullPath = newPath + (newPath.EndsWith("/") ?  "" : "/") + fileName;
                        newFullPath = newFullPath.TrimStart('/');

                        ApplicationModel.Current.AsyncOperations.Do(
                            () => ApplicationModel.Current.Client.RenameAsync(item.FullPath, newFullPath), 
                            string.Format("Moving '{0}' to '{1}'", fileName, newPath));
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

        private string ValidateFolderPath(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return "You must enter a path";
            }
            else if (!folderPath.StartsWith("/"))
            {
                return "The path must start with /";
            }
            else if (!Regex.IsMatch(folderPath, FolderPathRegEx))
            {
                return "Folder path contains invalid characters";
            }
            else
            {
                return "";
            }
        }
    }
}