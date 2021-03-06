using RavenFS.Studio.Infrastructure;

namespace RavenFS.Studio.Commands
{
	public class NavigateToCommand : Command
	{
		private string href;

		public override bool CanExecute(object parameter)
		{
			href = parameter as string;
			return href != null;
		}

		public override void Execute(object parameter)
		{
			UrlUtil.Navigate(href);
		}
	}
}