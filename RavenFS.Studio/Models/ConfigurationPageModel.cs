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
        public ObservableCollection<ConfigurationModel> AvailableConfigurations { get; private set; }
        public Observable<ConfigurationModel> SelectedConfiguration { get; private set; } 
        public Observable<NameValueCollectionEditorModel> ConfigurationSettings { get; private set; }

        private ICommand addNewConfiguration;
        private ICommand saveConfiguration;
        private ICommand deleteConfiguration;
        private ICommand discardChanges;

        public ICommand AddNewConfiguration { get { return addNewConfiguration ?? new ActionCommand(HandleAddNewConfiguration);  } }

        public ICommand SaveConfiguration
        {
            get
            {
                return saveConfiguration
                       ??
                       new ActionObservableDependantCommand<ConfigurationModel>(SelectedConfiguration,
                                                                                HandleSaveConfiguration,
                                                                                configuration => configuration != null && configuration.IsModified);
            }
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

        public ICommand DiscardChanges
        {
            get
            {
                return discardChanges
                       ?? new ActionObservableDependantCommand<ConfigurationModel>(SelectedConfiguration,
                                                                                   HandleDiscardChanges,
                                                                                   configuration => configuration != null && configuration.IsModified);
            }
        }

        public ConfigurationPageModel()
        {
            AvailableConfigurations = new ObservableCollection<ConfigurationModel>();

            SelectedConfiguration = new Observable<ConfigurationModel>() { Value = null };
            SelectedConfiguration.PropertyChanged += HandleSelectedConfigurationChanged;

            ConfigurationSettings = new Observable<NameValueCollectionEditorModel>();
        }

        private void HandleDiscardChanges(ConfigurationModel configuration)
        {
            AskUser.ConfirmationAsync(
                "Discard Changes",
                string.Format("Are you sure you want to discard the changes you have made to configuration '{0}'",configuration.Name))
                .ContinueWhenTrueInTheUIThread(() =>
                                                   {
                                                       ApplicationModel.Current.State.ModifiedConfigurations.Remove(
                                                           configuration.Name);
                                                       configuration.IsModified = false;
                                                       BeginEditConfiguration(configuration);
                                                   });


        }

        private void HandleAddNewConfiguration()
        {
            AskUser.QuestionAsync("Create Configuration", "Name")
                .ContinueOnUIThread(t =>
                                        {
                                            if (!t.IsCanceled)
                                            {
                                                var newName = t.Result;
                                                ApplicationModel.Current.State.ModifiedConfigurations.Add(newName, new NameValueCollection());
                                                BeginLoadConfigurations().ContinueOnSuccessInTheUIThread(() => SelectedConfiguration.Value = AvailableConfigurations.FirstOrDefault(c => c.Name.Equals(newName)));
                                            }
                                        });
        }

        private void HandleSaveConfiguration(ConfigurationModel configuration)
        {
            ApplicationModel.Current.AsyncOperations.Do(
                () =>
                SaveConfigurationAsync(configuration,
                                       ApplicationModel.Current.State.ModifiedConfigurations[configuration.Name]),
                string.Format("Saving configuration '{0}'", configuration));
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

        private void BeginEditConfiguration(ConfigurationModel configuration)
        {
            if (configuration != null)
            {
                if (ApplicationModel.Current.State.ModifiedConfigurations.ContainsKey(configuration.Name))
                {
                    EditConfigurationValues(configuration,
                                            ApplicationModel.Current.State.ModifiedConfigurations[configuration.Name]);
                }
                else
                {
                    ConfigurationSettings.Value = null;
                    ApplicationModel.Current.Client.Config.GetConfig(configuration.Name)
                        .ContinueOnUIThread(t => EditConfigurationValues(configuration, t.Result));
                }
            }
            else
            {
                ConfigurationSettings.Value = null;
            }
        }

        private void EditConfigurationValues(ConfigurationModel currentConfiguration, NameValueCollection settings)
        {
            if (SelectedConfiguration.Value != currentConfiguration)
            {
                return;
            }

            var editor = new NameValueCollectionEditorModel(settings);
            editor.Changed += delegate
                                                 {
                                                     ApplicationModel.Current
                                                         .State.ModifiedConfigurations[currentConfiguration.Name] = editor.GetCurrent();
                                                     currentConfiguration.IsModified = true;
                                                 };
            ConfigurationSettings.Value = editor;
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
