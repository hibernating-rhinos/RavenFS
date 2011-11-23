using System.ComponentModel;
using RavenFS.Studio.Infrastructure;

namespace RavenFS.Studio.Commands
{
	public class NavigateToNextPageCommand : Command
	{
		private readonly PagerModel pager;

		public NavigateToNextPageCommand(PagerModel pager)
		{
			this.pager = pager;
			this.pager.PropertyChanged += UpdateCanExecute;
		}

		private void UpdateCanExecute(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == "HasNextPage")
			{
				RaiseCanExecuteChanged();
			}
		}

		public override void Execute(object parameter)
		{
			pager.NavigateToNextPage();
		}

		public override bool CanExecute(object parameter)
		{
			return pager.HasNextPage();
		}
	}
}