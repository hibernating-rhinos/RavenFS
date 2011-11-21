using System;
using System.Windows;
using System.Windows.Input;
using RavenFS.Studio.Infrastructure.Input;
using RavenFS.Studio.Infrastructure;

namespace RavenFS.Studio.Commands
{
	public class DeleteCommand: ICommand
	{
		public string Name { get; set; }
		public DeleteCommand(string name)
		{
			Name = name;
		}
		public bool CanExecute(object parameter)
		{
			return true;
		}

		public void Execute(object parameter)
		{
			AskUser.ConfirmationAsync("Delete", "Are you sure you want to delete the file?").ContinueWhenTrueInTheUIThread(
				() =>
				{
					ApplicationModel.Client.DeleteAsync(Name);
					Application.Current.Host.NavigationState = "/home";
				});	
		}

		public event EventHandler CanExecuteChanged;
	}
}
