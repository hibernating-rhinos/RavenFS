using System;
using System.Collections.Generic;
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
        private IList<FileSystemModel> _folders;
        private string currentFolder;

        public string CurrentFolder
        {
            get { return currentFolder; }
            set
            {
                currentFolder = value;
                SetFolders(null);
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
                    return _folders != null ? _folders.Count : 0;
                }
            }
        }

        public override Task<IList<FileSystemModel>> GetPageAsync(int start, int pageSize)
        {
            lock (_folders)
            {
                if (_folders == null)
                {
                    return TaskEx.FromResult((IList<FileSystemModel>) (new FileSystemModel[0]));
                }

                var count = Math.Min(Math.Max(_folders.Count - start,0), pageSize);

                return TaskEx.FromResult((IList<FileSystemModel>)(_folders.Skip(start).Take(count).ToArray()));
            }
        }

        private void BeginGetFolders()
        {
            ApplicationModel.Current.Client.GetFoldersAsync(currentFolder, start: 0, pageSize: MaximumNumberOfFolders)
                .ContinueWith(t =>
                                  {
                                      var folders = t.Result.Select(n => new DirectoryModel() {FullPath = n}).ToArray();
                                      SetFolders(folders);
                                      OnCollectionChanged(EventArgs.Empty);
                                  });
        }

        private void SetFolders(DirectoryModel[] folders)
        {
            lock (_lock)
            {
                _folders = folders;
            }
        }
    }
}
