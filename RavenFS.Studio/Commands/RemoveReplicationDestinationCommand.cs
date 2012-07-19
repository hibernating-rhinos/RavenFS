using System;
using System.Linq;
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
using RavenFS.Studio.Models;

namespace RavenFS.Studio.Commands
{
    public class RemoveReplicationDestinationCommand : Command
    {
        public override void Execute(object parameter)
        {
            var destination = parameter as string;
            if (destination == null)
            {
                return;
            }

            ApplicationModel.Current.Client.Config.GetConfig(SynchronizationConstants.RavenSynchronizationDestinations)
                .ContinueWith(t =>
                                  {
                                      if (t.Result != null)
                                      {
                                          var config = t.Result;

                                          var newDestinationList = config.GetValues("url")
                                              .Where(v => !v.Equals(destination, StringComparison.InvariantCultureIgnoreCase));

                                          config.Remove("url");

                                          foreach (var value in newDestinationList)
                                          {
                                              config.Add("url", value);
                                          }

                                          ApplicationModel.Current.Client.Config.SetConfig(
                                              SynchronizationConstants.RavenSynchronizationDestinations, config).Catch();
                                      }
                                  })
                                  .Catch();
        }
    }
}
