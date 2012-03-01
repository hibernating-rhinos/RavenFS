using System;
using System.Windows;
using System.Windows.Input;
using RavenFS.Studio.Infrastructure.Input;
using RavenFS.Studio.Infrastructure;

namespace RavenFS.Studio.Commands
{
	public class DeleteCommand: Command
	{
		public string Name { get; set; }
		public DeleteCommand(string name)
		{
			Name = name;
		}

		public override void Execute(object parameter)
		{
			AskUser.ConfirmationAsync("Delete", "Are you sure you want to delete the file?").ContinueWhenTrueInTheUIThread(
				() =>
				{
					ApplicationModel.Current.Client.DeleteAsync(Name);
					Application.Current.Host.NavigationState = "/home";
				});	
		}
	}
}
