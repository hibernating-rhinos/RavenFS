using System.Windows;
using RavenFS.Studio.Infrastructure;

namespace RavenFS.Studio.Commands
{
	public class InfoCommand : Command
	{
		private readonly string fileName;

		public InfoCommand(string fileName)
		{
			this.fileName = fileName;
		}

		public override void Execute(object parameter)
		{
			Application.Current.Host.NavigationState = "/fileInfo?name=" + fileName;
		}
	}
}
