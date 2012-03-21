using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive.Concurrency;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Browser;
using System.Windows.Input;
using RavenFS.Client;
using RavenFS.Studio.Commands;
using RavenFS.Studio.Infrastructure;
using System.Linq;
using RavenFS.Studio.Extensions;
using System.Reactive.Linq;

namespace RavenFS.Studio.Models
{
	public class FilesPageModel : PageModel
	{
	    private const int DefaultPageSize = 50;

	    private ICommand downloadCommand;
	    private ICommand deleteCommand;
	    private ICommand editCommand;
	    private ICommand uploadCommand;
	    private ICommand navigateCommand;
	    private ICommand addFolderCommand;
        private FileSystemCollectionSource filesSource;

        public ICommand AddFolder { get { return addFolderCommand ?? (addFolderCommand = new AddFolderCommand(CurrentFolder)); } }
        public ICommand Navigate { get { return navigateCommand ?? (navigateCommand = new NavigateToFileSystemModelCommand()); } }
        public ICommand Upload { get { return uploadCommand ?? (uploadCommand = new UploadCommand(CurrentFolder)); } }
        public ICommand Download { get { return downloadCommand ?? (downloadCommand = new DownloadCommand(SelectedFile)); } }
        public ICommand Delete { get { return deleteCommand ?? (deleteCommand = new DeleteCommand(SelectedFile)); } }
        public ICommand EditProperties { get { return editCommand ?? (editCommand = new EditFilePropertiesCommand(SelectedFile)); } }

        public Observable<VirtualItem<FileSystemModel>> SelectedFile { get; private set; }
        public Observable<string> CurrentFolder { get; private set; } 
        public VirtualCollection<FileSystemModel> Files { get; private set; }
        public ObservableCollection<DirectoryModel> BreadcrumbTrail { get; private set; }

		public FilesPageModel()
		{
            filesSource = new FileSystemCollectionSource();
            Files = new VirtualCollection<FileSystemModel>(filesSource, DefaultPageSize);
            SelectedFile = new Observable<VirtualItem<FileSystemModel>>();
            CurrentFolder = new Observable<string>() { Value = "/"};
            CurrentFolder.PropertyChanged += delegate
                                                 {
                                                     filesSource.CurrentFolder = CurrentFolder.Value;
                                                     UpdateBreadCrumbs();
                                                     ApplicationModel.Current.Client.Notifications.FolderChanges(
                                                         CurrentFolder.Value)
                                                         .TakeUntil(Unloaded.Amb(CurrentFolder.ObserveChanged()))
                                                         .ObserveOn(DispatcherScheduler.Instance)
                                                         .Subscribe(_ => filesSource.Refresh());
                                                 };
            BreadcrumbTrail = new ObservableCollection<DirectoryModel>();
		}

	    private void UpdateBreadCrumbs()
	    {
            BreadcrumbTrail.Clear();

	        var folders = CurrentFolder.Value.Split(new[] {'/'}, StringSplitOptions.RemoveEmptyEntries);

	        var currentPath = "";

            foreach (var folder in folders)
            {
                currentPath += "/" + folder;
                BreadcrumbTrail.Add(new DirectoryModel() { FullPath = currentPath });
	        }
	    }

	    protected override void OnViewLoaded()
        {
            CurrentFolder.Value = GetFolder();
        }

	    private string GetFolder()
	    {
	        var folder = QueryParameters.GetValueOrDefault("folder", "");

	        folder = folder.TrimEnd('/');

            if (!folder.StartsWith("/"))
            {
                folder = "/" + folder;
            }
	        return folder;
	    }
	}
}
