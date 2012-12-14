using System;
using System.Collections.Generic;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using RavenFS.Client;
using RavenFS.Studio.Infrastructure;
using RavenFS.Studio.Infrastructure.Input;
using RavenFS.Studio.Models;

namespace RavenFS.Studio.Commands
{
    public class ResolveConflictWithLocalVersionCommand : VirtualItemSelectionCommand<ConflictItem>
    {
        public ResolveConflictWithLocalVersionCommand(ItemSelection<VirtualItem<ConflictItem>> itemSelection) : base(itemSelection)
        {
        }

        protected override bool CanExecuteOverride(IList<ConflictItem> items)
        {
            return items.Count > 0;
        }

        protected override void ExecuteOverride(IList<ConflictItem> items)
        {
            var message = items.Count == 1 ? string.Format("Are you sure you want to resolve the conflict for file '{0}' by choosing the local version?", items[0].FileName)
                : string.Format("Are you sure you want to resolve the conflict for {0} selected files by choosing the local version?", items.Count);

            AskUser.ConfirmationAsync("Resolve Conflict", message)
                .ContinueWhenTrueInTheUIThread(
                () =>
                {
                    foreach (var item in items)
                    {
                        var capturedItem = item;
                        ApplicationModel.Current.AsyncOperations.Do(
                            () => ApplicationModel.Current.Client.Synchronization.ResolveConflictAsync(item.FileName, ConflictResolutionStrategy.CurrentVersion),
                            "Resolving Conflict for " + capturedItem.FileName);
                    }

                });
        }
    }
}
