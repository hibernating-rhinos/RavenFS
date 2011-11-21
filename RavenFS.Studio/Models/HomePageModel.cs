using System.Collections.Generic;
using System.Windows.Input;
using RavenFS.Client;
using RavenFS.Studio.Commands;
using RavenFS.Studio.Infrastructure;
using System.Linq;

namespace RavenFS.Studio.Models
{
	public class HomePageModel : ModelBase
	{
		public ICommand Browse { get { return new BrowseCommand(); } }

		public BindableCollection<FileInfoWrapper> Files { get; set; }

		public HomePageModel()
		{
			Files = new BindableCollection<FileInfoWrapper>(EqualityComparer<FileInfoWrapper>.Default);

			ApplicationModel.Client.Browse()
				.ContinueWith(task => Files.Match(task.Result.Select(x=>new FileInfoWrapper(x)).ToList()));
		}
	}
}
