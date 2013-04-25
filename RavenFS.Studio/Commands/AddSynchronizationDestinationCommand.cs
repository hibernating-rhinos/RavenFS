using System;
using System.Threading.Tasks;
using RavenFS.Client;
using RavenFS.Studio.Infrastructure;
using RavenFS.Studio.Infrastructure.Input;
using RavenFS.Studio.Models;

namespace RavenFS.Studio.Commands
{
    public class AddSynchronizationDestinationCommand : Command
    {
        public override void Execute(object parameter)
        {
            var getConfigTask = ApplicationModel.Current.Client.Config.GetConfig(SynchronizationConstants.RavenSynchronizationDestinations);
            var getUsersResponseTask = AskUser.QuestionAsync("Add Replication Destination", "Url:", input => ValidateUrl(input), defaultAnswer:"http://");

            TaskEx.WhenAll(getConfigTask, getUsersResponseTask)
                .ContinueWith(_ =>
                                  {
                                      var config = getConfigTask.Result ?? new NameValueCollection();

	                                  config.Add("url", getUsersResponseTask.Result);

                                      ApplicationModel.Current.Client.Config.SetConfig(SynchronizationConstants.RavenSynchronizationDestinations, config);
                                  });
        }

        private string ValidateUrl(string input)
        {
            Uri result;
            return Uri.TryCreate(input, UriKind.Absolute, out result) ? "" : "You must enter a valid Uri";
        }
    }
}
