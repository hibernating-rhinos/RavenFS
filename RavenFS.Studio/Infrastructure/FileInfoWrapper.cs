using System.Windows.Input;
using RavenFS.Client;
using RavenFS.Studio.Commands;

namespace RavenFS.Studio.Infrastructure
{
	public class FileInfoWrapper
	{
		public FileInfoWrapper(FileInfo fileInfo)
		{
			File = fileInfo;
		}

		public FileInfo File { get; set; }

		public ICommand Info { get { return new InfoCommand(File.Name); } }
	}
}
