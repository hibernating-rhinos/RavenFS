using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using RavenFS.Studio.Commands;
using RavenFS.Studio.Infrastructure;
using System.Linq;

namespace RavenFS.Studio.Models
{
	public class FilesPageModel : PagerModel
	{
		public ICommand Upload { get { return new UploadCommand(TotalUploadFileSize, TotalBytesUploaded); } }
		public ICommand Download { get { return new DownloadCommand(); } }
		public PagerModel Pager { get; private set; }

		public Observable<long> TotalUploadFileSize { get; set; }
		public Observable<long> TotalBytesUploaded { get; set; }

		private Observable<long> NumberOfItems { get; set; }

		public BindableCollection<FileInfoWrapper> Files { get; set; }

		public FilesPageModel()
		{
			Files = new BindableCollection<FileInfoWrapper>(EqualityComparer<FileInfoWrapper>.Default);
			TotalBytesUploaded = new Observable<long>();
			TotalUploadFileSize = new Observable<long>();
			NumberOfItems = new Observable<long>();

			Pager = new PagerModel();
			Pager.SetTotalResults(NumberOfItems);
			Pager.Navigated += (sender, args) => TimerTickedAsync();
		}

		protected override System.Threading.Tasks.Task TimerTickedAsync()
		{
			return ApplicationModel.Client.BrowseAsync(Pager.Start, Pager.PageSize)
				.ContinueOnSuccess(result => Files.Match(result.Select(x => new FileInfoWrapper(x)).ToList()))
				.ContinueWith(_ => ApplicationModel.Client.StatsAsync())
				.ContinueOnSuccess(task=> NumberOfItems.Value = task.Result.FileCount);
		}
	}
}
