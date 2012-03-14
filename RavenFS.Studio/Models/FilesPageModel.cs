using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Browser;
using System.Windows.Input;
using RavenFS.Client;
using RavenFS.Studio.Commands;
using RavenFS.Studio.Infrastructure;
using System.Linq;
using RavenFS.Studio.Extensions;

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
	    private FilesCollectionSource filesSource;

        public ICommand Navigate { get { return navigateCommand ?? (navigateCommand = new NavigateToFileSystemModelCommand()); } }
        public ICommand Upload { get { return uploadCommand ?? (uploadCommand = new UploadCommand(CurrentFolder)); } }
        public ICommand Download { get { return downloadCommand ?? (downloadCommand = new DownloadCommand(SelectedFile)); } }
        public ICommand Delete { get { return deleteCommand ?? (deleteCommand = new DeleteCommand(SelectedFile)); } }
        public ICommand EditProperties { get { return editCommand ?? (editCommand = new EditFilePropertiesCommand(SelectedFile)); } }

        public Observable<VirtualItem<FileSystemModel>> SelectedFile { get; private set; }
        public Observable<string> CurrentFolder { get; private set; } 
        public VirtualCollection<FileSystemModel> Files { get; private set; }

		public FilesPageModel()
		{
		    filesSource = new FilesCollectionSource();
            Files = new VirtualCollection<FileSystemModel>(filesSource, DefaultPageSize);
            SelectedFile = new Observable<VirtualItem<FileSystemModel>>();
            CurrentFolder = new Observable<string>() { Value = "/"};
            CurrentFolder.PropertyChanged += delegate { filesSource.CurrentFolder = CurrentFolder.Value; };
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

	    protected override Task TimerTickedAsync()
		{
		    return Completed;
		}
	}
}
