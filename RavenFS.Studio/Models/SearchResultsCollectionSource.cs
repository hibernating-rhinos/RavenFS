using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
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
using RavenFS.Studio.Infrastructure;
using RavenFS.Studio.Extensions;

namespace RavenFS.Studio.Models
{
    public class SearchResultsCollectionSource : VirtualCollectionSource<FileSystemModel>
    {
        public SearchResultsCollectionSource()
        {
            
        } 

        private object lockObject = new object();
        private int fileCount;
        private string searchPattern;

        public override int Count
        {
            get
            {
                lock (lockObject)
                {
                    return fileCount;
                }
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
                OnCollectionChanged(new VirtualCollectionChangedEventArgs(InterimDataMode.Clear));
                Refresh();
            }
        }

        public void Refresh()
        {
            BeginGetCount();
        }

        public override Task<IList<FileSystemModel>> GetPageAsync(int start, int pageSize, IList<SortDescription> sortDescriptions)
        {
            return ApplicationModel.Current.Client.SearchAsync(searchPattern, null, start: start, pageSize: pageSize)
                        .ContinueWith(t =>
                                          {
                                              var result = (IList<FileSystemModel>) ToFileSystemModels(t.Result.Files).Take(pageSize).ToArray();
                                              UpdateCount(t.Result.FileCount);
                                              return result;
                                          });
        }

        private FilesSortOptions MapSortDescription(IList<SortDescription> sortDescriptions)
        {
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



        private void BeginGetCount()
        {
            if (searchPattern.IsNullOrEmpty())
            {
                UpdateCount(0, forceCollectionRefresh: true);
            }
            else
            {
                ApplicationModel.Current.Client.SearchAsync(searchPattern, null, pageSize: 1)
                    .ContinueWith(t =>
                                  UpdateCount(t.Result.FileCount, forceCollectionRefresh: true),
                                  TaskContinuationOptions.ExecuteSynchronously);
            }
        }

        private void UpdateCount(int newCount, bool forceCollectionRefresh = false)
        {
            bool fileCountChanged; 

            lock(lockObject)
            {
                fileCountChanged = newCount != fileCount;
                fileCount = newCount;
            }

            if (fileCountChanged || forceCollectionRefresh)
            {
                OnCollectionChanged(new VirtualCollectionChangedEventArgs(InterimDataMode.ShowStaleData));
            }
        }
    }
}
