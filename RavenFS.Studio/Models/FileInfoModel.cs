using System.Windows.Input;
using RavenFS.Client;
using RavenFS.Studio.Commands;
using RavenFS.Studio.Infrastructure;

namespace RavenFS.Studio.Models
{
	public class FileInfoModel : ModelBase
	{
		public string Name { get; set; }

		public FileInfoModel()
		{
			Name = GetQueryParam("name");

			ApplicationModel.Client.GetMetadataForAsync(Name)
				.ContinueWith(task => Metadata = task.Result);
		}

		private NameValueCollection metadata;

		public NameValueCollection Metadata
		{
			get { return metadata; }
			set { metadata = value; OnPropertyChanged();}
		}

		public ICommand Download { get { return new DownloadCommand(Name); } }
	}
}