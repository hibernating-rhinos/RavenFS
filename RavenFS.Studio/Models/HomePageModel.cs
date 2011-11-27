using System.Collections.Generic;
using System.Windows.Input;
using RavenFS.Studio.Commands;
using RavenFS.Studio.Infrastructure;
using System.Linq;

namespace RavenFS.Studio.Models
{
	public class HomePageModel : PagerModel
	{
		public ICommand Upload { get { return new UploadCommand(); } }
		public ICommand Download { get { return new DownloadCommand(); } }
		public PagerModel Pager { get; private set; }

		private Observable<long> NumberOfItems { get; set; }

		public BindableCollection<FileInfoWrapper> Files { get; set; }

		public HomePageModel()
		{
			Files = new BindableCollection<FileInfoWrapper>(EqualityComparer<FileInfoWrapper>.Default);

			NumberOfItems = new Observable<long>();

			Pager = new PagerModel();
			Pager.SetTotalResults(NumberOfItems);
			Pager.Navigated += (sender, args) => ForceTimerTicked();
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
