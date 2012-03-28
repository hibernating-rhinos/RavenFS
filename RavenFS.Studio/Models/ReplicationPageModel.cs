using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Net;
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
using RavenFS.Studio.Extensions;
using RavenFS.Studio.Infrastructure;
using RavenFS.Studio.Infrastructure.Input;

namespace RavenFS.Studio.Models
{
    public class ReplicationPageModel : PageModel
    {
        public ObservableCollection<string> AvailableConfigurations { get; private set; }
        public Observable<string> SelectedConfiguration { get; private set; } 
        public Observable<NameValueCollectionEditorModel> ConfigurationSettings { get; private set; }

        private ICommand addNewConfiguration;
        private ICommand saveConfiguration;
        private ICommand deleteConfiguration;

        public ICommand AddNewConfiguration { get { return addNewConfiguration ?? new ActionCommand(HandleAddNewConfiguration);  } }
        public ICommand SaveConfiguration { get { return saveConfiguration ?? new ActionCommand(HandleSaveConfiguration);  } }
        public ICommand DeleteConfiguration { get { return deleteConfiguration ?? new ActionCommand(HandleDeleteConfiguration);  } }

        public string lastSelectedConfiguration;

        public ReplicationPageModel()
        {
            AvailableConfigurations = new ObservableCollection<string>();

            SelectedConfiguration = new Observable<string>() { Value = ""};
            SelectedConfiguration.PropertyChanged += HandleSelectedConfigurationChanged;

            ConfigurationSettings = new Observable<NameValueCollectionEditorModel>();
        }

        private void HandleAddNewConfiguration()
        {
            AskUser.QuestionAsync("Create Configuration", "Name")
                .ContinueOnUIThread(t =>
                                        {
                                            if (!t.IsCanceled)
                                            {
                                                ApplicationModel.Current.ModifiedConfigurations.Add(t.Result, new NameValueCollection());
                                                AvailableConfigurations.Add(t.Result);
                                                SelectedConfiguration.Value = t.Result;
                                            }
                                        });
        }

        private void HandleSaveConfiguration()
        {
            if (SelectedConfiguration.Value.IsNullOrEmpty() 
                || !ApplicationModel.Current.ModifiedConfigurations.ContainsKey(SelectedConfiguration.Value))
            {
                return;
            }

            ApplicationModel.Current.AsyncOperations.Do(() =>
                                                            {
                                                                string configName = SelectedConfiguration.Value;
                                                                return SaveConfigurationAsync(configName, ApplicationModel.Current.ModifiedConfigurations[configName]);
                                                            }, string.Format("Saving configuration '{0}'", SelectedConfiguration.Value));
        }

        private void HandleDeleteConfiguration()
        {
            string currentConfiguration = SelectedConfiguration.Value;

            if (currentConfiguration.IsNullOrEmpty())
            {
                return;
            }

            AskUser.ConfirmationAsync(
                "Delete Configuration",
                string.Format("Are you sure you want to delete configuration '{0}'?", currentConfiguration))
                .ContinueWhenTrueInTheUIThread(
                    () => ApplicationModel.Current.AsyncOperations.Do(
                        () => ApplicationModel.Current.Client.Config.DeleteConfig(currentConfiguration),
                        string.Format("Deleting configuration '{0}'", currentConfiguration)));
        }

        private Task SaveConfigurationAsync(string configName, NameValueCollection configValues)
        {
            return ApplicationModel.Current.Client.Config.SetConfig(configName, configValues)
                .ContinueOnUIThread(t =>
                                        {
                                            if (t.Status == TaskStatus.RanToCompletion)
                                            {
                                                ApplicationModel.Current.ModifiedConfigurations.Remove(configName);
                                            }
                                        });
        }

        private void HandleSelectedConfigurationChanged(object sender, PropertyChangedEventArgs e)
        {
            string currentConfiguration = SelectedConfiguration.Value;
            if (!string.IsNullOrEmpty(currentConfiguration))
            {
                if (ApplicationModel.Current.ModifiedConfigurations.ContainsKey(currentConfiguration))
                {
                    EditConfigurationValues(currentConfiguration, ApplicationModel.Current.ModifiedConfigurations[currentConfiguration]);
                }
                else
                {
                    ConfigurationSettings.Value = null;
                    ApplicationModel.Current.Client.Config.GetConfig(currentConfiguration)
                        .ContinueOnUIThread(t => EditConfigurationValues(currentConfiguration, t.Result));
                }
            }
        }

        private void EditConfigurationValues(string currentConfiguration, NameValueCollection settings)
        {
            if (SelectedConfiguration.Value != currentConfiguration)
            {
                return;
            }

            ConfigurationSettings.Value = new NameValueCollectionEditorModel(settings);
            ConfigurationSettings.Value.Changed += delegate
                                                 {
                                                     ApplicationModel.Current
                                                         .ModifiedConfigurations[currentConfiguration] = ConfigurationSettings.Value.GetCurrent();
                                                 };
        }


        protected override void OnViewLoaded()
        {
            BeginLoadConfigurations();
        }

        private void BeginLoadConfigurations()
        {
            ApplicationModel.Current.Client.Config
                .GetConfigNames(pageSize: 1024)
                .ContinueOnUIThread(UpdateUIWithConfigurations);
        }

        private void UpdateUIWithConfigurations(Task<string[]> configurations)
        {
            var currentConfiguration = SelectedConfiguration.Value;

            AvailableConfigurations.Clear();
            AvailableConfigurations.AddRange(ApplicationModel.Current.ModifiedConfigurations.Keys);
            AvailableConfigurations.AddRange(configurations.Result);

            SelectedConfiguration.Value = currentConfiguration;
            if (string.IsNullOrEmpty(SelectedConfiguration.Value))
            {
                SelectedConfiguration.Value = AvailableConfigurations.FirstOrDefault();
            }
        }
    }
}
