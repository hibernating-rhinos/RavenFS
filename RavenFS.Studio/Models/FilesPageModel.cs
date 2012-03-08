using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Browser;
using System.Windows.Input;
using RavenFS.Client;
using RavenFS.Studio.Commands;
using RavenFS.Studio.Infrastructure;
using System.Linq;

namespace RavenFS.Studio.Models
{
	public class FilesPageModel : Model
	{
	    private const int DefaultPageSize = 50;

	    private ICommand downloadCommand;
	    private ICommand deleteCommand;
	    private ICommand editCommand;

	    public ICommand Upload { get { return new UploadCommand(); } }
        public ICommand Download { get { return downloadCommand ?? (downloadCommand = new DownloadCommand(SelectedFile)); } }
        public ICommand Delete { get { return deleteCommand ?? (deleteCommand = new DeleteCommand(SelectedFile)); } }
        public ICommand EditProperties { get { return editCommand ?? (editCommand = new EditFilePropertiesCommand(SelectedFile)); } }

        public Observable<VirtualItem<FileInfo>> SelectedFile { get; private set; }

		public VirtualCollection<FileInfo> Files { get; private set; }

		public FilesPageModel()
		{
			Files = new VirtualCollection<FileInfo>(DefaultPageSize)
			            {
                            RowCountFetcher = () => ApplicationModel.Current.Client.StatsAsync().ContinueOnSuccess(t => (int)t.FileCount),
                            PageFetcher = (start, pageSize) => ApplicationModel.Current.Client.BrowseAsync(start, pageSize).ContinueOnSuccess(t => (IList<FileInfo>)t)
			            };

		    SelectedFile = new Observable<VirtualItem<FileInfo>>();
		}

		protected override Task TimerTickedAsync()
		{
            Files.Refresh();
		    return Completed;
		}
	}
}
