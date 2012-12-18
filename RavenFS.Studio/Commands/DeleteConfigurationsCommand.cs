using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using RavenFS.Client;
using RavenFS.Studio.Infrastructure.Input;
using RavenFS.Studio.Infrastructure;
using RavenFS.Studio.Models;

namespace RavenFS.Studio.Commands
{
    public class DeleteConfigurationsCommand : VirtualItemSelectionCommand<ConfigurationModel>
	{
        public DeleteConfigurationsCommand(ItemSelection<VirtualItem<ConfigurationModel>> itemSelection)
            : base(itemSelection)
		{
		}

        protected override bool CanExecuteOverride(IList<ConfigurationModel> items)
        {
            return items.Any();
        }

        protected override void ExecuteOverride(IList<ConfigurationModel> items)
        {
            var message = items.Count == 1 ? string.Format("Are you sure you want to delete configuration '{0}'?", items[0].Name)
                : string.Format("Are you sure you want to delete {0} selected configurations?", items.Count);

            AskUser.ConfirmationAsync("Delete", message)
                .ContinueWhenTrueInTheUIThread(
				() =>
				    {
				        foreach (var item in items)
				        {
				            var capturedItem = item;
				            ApplicationModel.Current.AsyncOperations.Do(
				                () => ApplicationModel.Current.Client.Config.DeleteConfig(capturedItem.Name),
				                "Deleting " + capturedItem.Name);
				        }

				    });
        }
	}
}
