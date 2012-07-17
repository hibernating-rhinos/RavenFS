using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using RavenFS.Client;
using RavenFS.Studio.Extensions;
using RavenFS.Studio.Infrastructure;
using RavenFS.Extensions;

namespace RavenFS.Studio.Models
{
    public class FilesCollectionSource : VirtualCollectionSource<FileSystemModel>
    {
        private string currentFolder;
        private string searchPattern;

        public FilesCollectionSource()
        {
            currentFolder = "/";
        }

        public string CurrentFolder
        {
            get { return currentFolder; }
            set
            {
                currentFolder = value;
                Refresh(RefreshMode.ClearStaleData);
            }
        }

        public string SearchPattern
        {
            get { return searchPattern; }
            set
            {
                if (searchPattern == value)
                {
                    return;
                }
                searchPattern = value;
                Refresh(RefreshMode.ClearStaleData);
            }
        }

        protected override Task<int> GetCount()
        {
            return DoQuery(0, 0, null).ContinueWith(t => t.Result.FileCount,
                TaskContinuationOptions.ExecuteSynchronously);
        }

        protected override Task<IList<FileSystemModel>> GetPageAsyncOverride(int start, int pageSize, IList<SortDescription> sortDescriptions)
        {
            return DoQuery(start, pageSize, sortDescriptions)
                        .ContinueWith(t =>
                                          {
                                              var result = (IList<FileSystemModel>) ToFileSystemModels(t.Result.Files).Take(pageSize).ToArray();
                                              SetCount(t.Result.FileCount);
                                              return result;
                                          });
        }

        private Task<SearchResults> DoQuery(int start, int pageSize, IList<SortDescription> sortDescriptions)
        {
            return ApplicationModel.Current.Client.GetFilesAsync(currentFolder, MapSortDescription(sortDescriptions), fileNameSearchPattern:searchPattern, start: start, pageSize: pageSize);
        }

        private FilesSortOptions MapSortDescription(IList<SortDescription> sortDescriptions)
        {
            if (sortDescriptions == null)
            {
                return FilesSortOptions.Default;
            }

            var sortDescription = sortDescriptions.FirstOrDefault();

            FilesSortOptions sort = FilesSortOptions.Default;

            if (sortDescription.PropertyName == "Name")
            {
                sort = FilesSortOptions.Name;
            }
            else if (sortDescription.PropertyName == "Size")
            {
                sort = FilesSortOptions.Size;
            }
            else if (sortDescription.PropertyName == "LastModified")
            {
                sort = FilesSortOptions.LastModified;
            }

            if (sortDescription.Direction == ListSortDirection.Descending)
            {
                sort |= FilesSortOptions.Desc;
            }

            return sort;
        }

        private static IEnumerable<FileSystemModel> ToFileSystemModels(IEnumerable<FileInfo> files)
        {
            return files
                .Where(fi => fi != null)
                .Select(fi => new FileModel
                                  {
                                      FormattedTotalSize = fi.HumaneTotalSize,
                                      FullPath = fi.Name,
                                      Metadata = fi.Metadata
                                  });
        }
    }
}
