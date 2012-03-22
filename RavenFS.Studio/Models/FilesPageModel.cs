using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Browser;
using System.Windows.Input;
using Microsoft.Expression.Interactivity.Core;
using RavenFS.Client;
using RavenFS.Studio.Commands;
using RavenFS.Studio.Infrastructure;
using System.Linq;
using RavenFS.Studio.Extensions;
using System.Reactive.Linq;
using ActionCommand = RavenFS.Studio.Infrastructure.ActionCommand;

namespace RavenFS.Studio.Models
{
	public class FilesPageModel : PageModel
	{
	    private const int DefaultPageSize = 50;
        private const string SearchPatternValidationRegEx = @"^[\*\?\w|\-\.\s]*$";
	    private ICommand downloadCommand;
	    private ICommand deleteCommand;
	    private ICommand editCommand;
	    private ICommand uploadCommand;
	    private ICommand navigateCommand;
	    private ICommand addFolderCommand;
	    private ICommand renameFileCommand;
	    private ICommand clearSearchCommand;
	    private ICommand showSearchCommand;
        private FileSystemCollectionSource filesSource;

        public ICommand RenameFile { get { return renameFileCommand ?? (renameFileCommand = new RenameFileCommand(SelectedFile)); } }
        public ICommand AddFolder { get { return addFolderCommand ?? (addFolderCommand = new AddFolderCommand(CurrentFolder)); } }
        public ICommand Navigate { get { return navigateCommand ?? (navigateCommand = new NavigateToFileSystemModelCommand()); } }
        public ICommand Upload { get { return uploadCommand ?? (uploadCommand = new UploadCommand(CurrentFolder)); } }
        public ICommand Download { get { return downloadCommand ?? (downloadCommand = new DownloadCommand(SelectedFile)); } }
        public ICommand Delete { get { return deleteCommand ?? (deleteCommand = new DeleteCommand(SelectedFile)); } }
        public ICommand EditProperties { get { return editCommand ?? (editCommand = new EditFilePropertiesCommand(SelectedFile)); } }
	    
        public ICommand ShowSearch
	    {
	        get
	        {
	            return showSearchCommand ?? (showSearchCommand = new ActionCommand(() => IsSearchVisible.Value = true));
	        }
	    }
	    
        public ICommand ClearSearch
	    {
	        get
	        {
	            return clearSearchCommand ??
	                   (clearSearchCommand = new ActionCommand(
	                                             () =>
	                                                 {
	                                                     SearchPattern.Value = "";
	                                                     IsSearchVisible.Value = false;
	                                                 }
	                                             ));
	        }
	    }

	    public Observable<VirtualItem<FileSystemModel>> SelectedFile { get; private set; }
        public Observable<string> CurrentFolder { get; private set; } 
        public VirtualCollection<FileSystemModel> Files { get; private set; }
        public ObservableCollection<DirectoryModel> BreadcrumbTrail { get; private set; }
        public Observable<string> SearchPattern { get; private set; }
        public Observable<bool> IsSearchVisible { get; private set; }

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
                                                         .TakeUntil(Unloaded.Amb(CurrentFolder.ObserveChanged().Select(_ => Unit.Default)))
                                                         .Throttle(TimeSpan.FromSeconds(1))
                                                         .ObserveOn(DispatcherScheduler.Instance)
                                                         .Subscribe(_ => filesSource.Refresh());
                                                 };

            SearchPattern = new Observable<string>() { Value=""};
		    SearchPattern.ObserveChanged().Throttle(TimeSpan.FromSeconds(1)).Where(SearchPatternIsValid).Subscribe(value => filesSource.SearchPattern = value);
            
            IsSearchVisible = new Observable<bool>();

            BreadcrumbTrail = new ObservableCollection<DirectoryModel>();
		}

        private bool SearchPatternIsValid(string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                return true;
            }

            var startsWithWildcard = pattern.StartsWith("*") || pattern.StartsWith("?");
            var endsWithWildcard = pattern.EndsWith("*") || pattern.EndsWith("?");

            if (startsWithWildcard && endsWithWildcard)
            {
                return false;
            }

            return Regex.IsMatch(pattern, SearchPatternValidationRegEx);
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
