using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace RavenFS.Studio.Models
{
    /// <summary>
    /// Manages a list of folders which the user has created in the client, but which
    /// do not yet exist on the server
    /// </summary>
    public class VirtualFoldersManager
    {
        private ObservableCollection<DirectoryModel> folders = new ObservableCollection<DirectoryModel>();
 
        public VirtualFoldersManager()
        {
            VirtualFolders = new ReadOnlyObservableCollection<DirectoryModel>(folders);
        }

        public void Add(string path)
        {
            if (!path.StartsWith("/"))
            {
                throw new ArgumentException("Folder path must start with /");
            }

            if (path.EndsWith("/"))
            {
                throw new ArgumentException("Folder path must not end with /");
            }    

            if (folders.Any(d => d.FullPath.Equals(path, StringComparison.CurrentCultureIgnoreCase)))
            {
                return;
            }

            folders.Add(new DirectoryModel() { FullPath = path, IsVirtual=true});
        }

        public void PruneFoldersThatNowExist(IList<DirectoryModel> realFolders)
        {
            var newlyRealisedFolders =
                folders.Where(f => RealFolderHasSamePathAsVirtualFolder(realFolders, f))
                    .ToList();

            foreach (var folder in newlyRealisedFolders)
            {
                folders.Remove(folder);
            }
        }

        /// <summary>
        /// Gets the 
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public IList<DirectoryModel> GetSubFolders(string path)
        {
            var subFolders =
                folders.Where(f => FolderIsSubPath(path, f.FullPath))
                .Select(f => GetImmediateSubFolderPath(f.FullPath, path))
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .Select(f => new DirectoryModel() { FullPath = f, IsVirtual = true})
                .ToList();

            return subFolders;
        }

        private static bool FolderIsSubPath(string parentPath, string folderPath)
        {
            return (folderPath.Length > parentPath.Length) && folderPath.StartsWith(parentPath, StringComparison.InvariantCultureIgnoreCase);
        }

        private string GetImmediateSubFolderPath(string fullPath, string parentPath)
        {
            var folderName = fullPath.Substring(parentPath.Length);
            var indexOfSlash = folderName.IndexOf('/',1);
            if (indexOfSlash > 0)
            {
                folderName = folderName.Substring(0, indexOfSlash);
            }

            return parentPath + folderName;
        }

        private static bool RealFolderHasSamePathAsVirtualFolder(IList<DirectoryModel> realFolders, DirectoryModel f)
        {
            return realFolders.Any(realFolder => realFolder.FullPath.StartsWith(f.FullPath, StringComparison.InvariantCultureIgnoreCase));
        }

        public ReadOnlyObservableCollection<DirectoryModel> VirtualFolders { get; private set; }
    }
}
