using System.Collections.Generic;
using System.Windows.Input;
using RavenFS.Client;
using RavenFS.Studio.Commands;
using RavenFS.Studio.Infrastructure;

namespace RavenFS.Studio.Models
{
	public class HomePageModel : ModelBase
	{
		public ICommand Browse { get { return new BrowseCommand(); } }

		public BindableCollection<FileInfo> Files { get; set; }

		public HomePageModel()
		{
			Files = new BindableCollection<FileInfo>(EqualityComparer<FileInfo>.Default);

			ApplicationModel.Client.Browse()
				.ContinueWith(task => Files.Match(task.Result));
			
		}
	}
}
