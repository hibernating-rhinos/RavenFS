using System;
using System.Collections.Generic;
using System.Collections.Specialized;
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
using RavenFS.Studio.Infrastructure;
using RavenFS.Studio.Extensions;

namespace RavenFS.Studio.Models
{
    public class FoldersCollectionSource : VirtualCollectionSource<FileSystemModel>
    {
        private const int MaximumNumberOfFolders = 1024;
        private readonly object _lock = new object();
        private IList<FileSystemModel> folders;
        private IList<FileSystemModel> virtualFolders; 
        private string currentFolder;
        private TaskScheduler synchronizationContextScheduler = TaskScheduler.FromCurrentSynchronizationContext();
        private bool isPruningFolders;

        public FoldersCollectionSource()
        {
            ApplicationModel.Current.VirtualFolders.VirtualFolders
                .ObserveCollectionChanged()
                .SubscribeWeakly(this, (t, e) => t.HandleVirtualFoldersChanged(e));    
        }

        private void HandleVirtualFoldersChanged(NotifyCollectionChangedEventArgs e)
        {
            if (!isPruningFolders)
            {
                UpdateVirtualFolders();
                OnCollectionChanged(EventArgs.Empty);
            }
        }

        private void UpdateVirtualFolders()
        {
            lock (_lock)
            {
                virtualFolders =
                    ApplicationModel.Current.VirtualFolders.GetSubFolders(currentFolder).Cast<FileSystemModel>().ToList();
            }
        }

        public string CurrentFolder
        {
            get { return currentFolder; }
            set
            {
                currentFolder = value;
                SetFolders(null);
                UpdateVirtualFolders();
                BeginGetFolders();
            }
        }

        public void Refresh()
        {
            BeginGetFolders();
        }

        public override int Count
        {
            get
            {
                lock (_lock)
                {
                    var foldersCount = folders != null ? folders.Count : 0;
                    var virtualFoldersCount = virtualFolders != null ? virtualFolders.Count : 0;

                    return foldersCount + virtualFoldersCount;
                }
            }
        }

        public override Task<IList<FileSystemModel>> GetPageAsync(int start, int pageSize, IList<SortDescription> sortDescriptions)
        {
            lock (_lock)
            {
                if (folders == null && virtualFolders == null)
                {
                    return TaskEx.FromResult((IList<FileSystemModel>) (new FileSystemModel[0]));
                }

                var count = Math.Min(Math.Max(Count- start,0), pageSize);

                return TaskEx.FromResult((IList<FileSystemModel>)(virtualFolders.Concat(folders).Apply(e => ApplySort(e, sortDescriptions)).Skip(start).Take(count).ToArray()));
            }
        }

        private IEnumerable<FileSystemModel> ApplySort(IEnumerable<FileSystemModel> files, IList<SortDescription> sortDescriptions)
        {
            var sort = sortDescriptions.FirstOrDefault();

            if (sort.PropertyName == "Name" && sort.Direction == ListSortDirection.Ascending)
            {
                return files.OrderBy(f => f.Name);
            }
            else if (sort.PropertyName == "Name" && sort.Direction == ListSortDirection.Descending)
            {
                return files.OrderByDescending(f => f.Name);
            }
            else
            {
                return files;
            }
        }

        private void BeginGetFolders()
        {
            if (string.IsNullOrEmpty(currentFolder))
            {
                return;
            }

            ApplicationModel.Current.Client.GetFoldersAsync(currentFolder, start: 0, pageSize: MaximumNumberOfFolders)
                .ContinueWith(t =>
                                  {
                                      var folders = t.Result.Select(n => new DirectoryModel() {FullPath = n}).ToArray();
                                      PruneVirtualFolders(folders);
                                      SetFolders(folders);
                                      OnCollectionChanged(EventArgs.Empty);
                                  }, synchronizationContextScheduler);
        }

        private void PruneVirtualFolders(DirectoryModel[] folders)
        {
            isPruningFolders = true;
            ApplicationModel.Current.VirtualFolders.PruneFoldersThatNowExist(folders);
            isPruningFolders = false;

            UpdateVirtualFolders();
        }

        private void SetFolders(DirectoryModel[] folders)
        {
            lock (_lock)
            {
                this.folders = folders;
            }
        }
    }
}
