using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using RavenFS.Client;
using RavenFS.Extensions;
using RavenFS.Studio.Commands;
using RavenFS.Studio.Extensions;
using RavenFS.Studio.Infrastructure;
using RavenFS.Studio.Infrastructure.Input;

namespace RavenFS.Studio.Models
{
    public class ConfigurationPageModel : PageModel
    {
        private ICommand addNewConfiguration;
        private ICommand saveConfiguration;
        private ICommand deleteConfiguration;
        private ICommand editConfiguration;

        public ICommand AddNewConfiguration { get { return addNewConfiguration ?? (addNewConfiguration = new ActionCommand(HandleAddNewConfiguration));  } }

        public ICommand EditConfiguration { get { return editConfiguration ?? (editConfiguration = new ActionCommand(HandleEditConfiguration)); } }

        private void HandleEditConfiguration(object parameter)
        {
            var configuration = parameter as ConfigurationModel;
            if (configuration == null)
            {
                return;
            }

            UrlUtil.Navigate("/EditConfiguration?name=" + configuration.Name);
        }

        public ICommand DeleteConfiguration
        {
            get
            {
                return deleteConfiguration ??
                       new ActionObservableDependantCommand<ConfigurationModel>(SelectedConfiguration,
                                                                                HandleDeleteConfiguration,
                                                                                configuration => configuration != null);
            }
        }

       

        public ConfigurationPageModel()
        {

        }

        private void HandleAddNewConfiguration()
        {
            UrlUtil.Navigate("/EditConfiguration");
        }

       
        private void HandleDeleteConfiguration(ConfigurationModel configuration)
        {
            AskUser.ConfirmationAsync(
                "Delete Configuration",
                string.Format("Are you sure you want to delete configuration '{0}'?", configuration.Name))
                .ContinueWhenTrueInTheUIThread(
                    () =>
                        {
                            AvailableConfigurations.Remove(configuration);
                            ApplicationModel.Current.AsyncOperations.Do(
                                () => DeleteConfigurationAsync(configuration),
                                string.Format("Deleting configuration '{0}'", configuration));
                        });
        }

        private static Task DeleteConfigurationAsync(ConfigurationModel configuration)
        {
            return ApplicationModel.Current.Client.Config.DeleteConfig(configuration.Name)
                .ContinueOnSuccessInTheUIThread(() => ApplicationModel.Current.State.ModifiedConfigurations.Remove(configuration.Name));
        }

        private Task SaveConfigurationAsync(ConfigurationModel configuration, NameValueCollection configValues)
        {
            return ApplicationModel.Current.Client.Config.SetConfig(configuration.Name, configValues)
                .ContinueOnUIThread(t =>
                                        {
                                            if (t.Status == TaskStatus.RanToCompletion)
                                            {
                                                ApplicationModel.Current.State.ModifiedConfigurations.Remove(configuration.Name);
                                                configuration.IsModified = false;
                                            }
                                        });
        }

        private void HandleSelectedConfigurationChanged(object sender, PropertyChangedEventArgs e)
        {
            BeginEditConfiguration(SelectedConfiguration.Value);
        }

      

        protected override void OnViewLoaded()
        {
            BeginLoadConfigurations();
            ApplicationModel.Current.Client.Notifications
                .ConfigurationChanges()
                .Throttle(TimeSpan.FromSeconds(1))
                .TakeUntil(Unloaded)
                .ObserveOn(DispatcherScheduler.Instance)
                .Subscribe(_ => BeginLoadConfigurations());
        }

        private Task BeginLoadConfigurations()
        {
            return ApplicationModel.Current.Client.Config
                .GetConfigNames(pageSize: 1024)
                .ContinueOnUIThread(UpdateUIWithConfigurations);
        }

        private void UpdateUIWithConfigurations(Task<string[]> configurations)
        {
            AvailableConfigurations.UpdateFromOrdered(
                ApplicationModel.Current.State.ModifiedConfigurations.Keys
                    .Concat(configurations.Result)
                    .Distinct()
                    .OrderBy(x => x)
                    .Select(n => new ConfigurationModel(n) { IsModified = ApplicationModel.Current.State.ModifiedConfigurations.ContainsKey(n) }),
                    m => m.Name.ToLowerInvariant());


            if (SelectedConfiguration.Value == null)
            {
                SelectedConfiguration.Value = AvailableConfigurations.FirstOrDefault();
            }
        }
    }
}
