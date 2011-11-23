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

		public BindableCollection<FileInfoWrapper> Files { get; set; }

		public HomePageModel()
		{
			Files = new BindableCollection<FileInfoWrapper>(EqualityComparer<FileInfoWrapper>.Default);

			var NumberOfItems = new Observable<long>();
			NumberOfItems.Value = 6;

			Pager = new PagerModel();
			Pager.SetTotalResults(NumberOfItems);
			Pager.Navigated += (sender, args) => ForceTimerTicked();		
		}

		protected override System.Threading.Tasks.Task TimerTickedAsync()
		{
			return ApplicationModel.Client.BrowseAsync(Pager.CurrentPage,Pager.PageSize)
				.ContinueOnSuccess(result => Files.Match(result.Select(x => new FileInfoWrapper(x)).ToList()));
		}
	}
}
