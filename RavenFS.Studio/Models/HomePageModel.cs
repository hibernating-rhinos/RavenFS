using System.Collections.Generic;
using System.Windows.Input;
using RavenFS.Studio.Commands;
using RavenFS.Studio.Infrastructure;
using System.Linq;

namespace RavenFS.Studio.Models
{
	public class HomePageModel : Model
	{
		public ICommand Upload { get { return new UploadCommand(); } }
		public ICommand Download { get { return new DownloadCommand(); } }

		public BindableCollection<FileInfoWrapper> Files { get; set; }

		public HomePageModel()
		{
			Files = new BindableCollection<FileInfoWrapper>(EqualityComparer<FileInfoWrapper>.Default);

			ForceTimerTicked();
		}

		protected override System.Threading.Tasks.Task TimerTickedAsync()
		{
			return ApplicationModel.Client.BrowseAsync()
				.ContinueOnSuccess(result => Files.Match(result.Select(x => new FileInfoWrapper(x)).ToList()));
		}
	}
}
