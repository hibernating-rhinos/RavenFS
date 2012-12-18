using System;
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
using RavenFS.Studio.Commands;
using RavenFS.Studio.Extensions;
using RavenFS.Studio.Infrastructure;
using RavenFS.Studio.Infrastructure.Input;

namespace RavenFS.Studio.Models
{
    public class EditConfigurationModel : PageModel
    {
        private ICommand _saveCommand;
        private ICommand _deleteCommand;
        private ICommand _refreshCommand;
        public Observable<NameValueCollectionEditorModel> ConfigurationSettings { get; private set; }
        public Observable<string> Name { get; private set; }
        public Observable<string> OriginalName { get; private set; }
        private bool hasUnsavedChanges;

        public EditConfigurationModel()
        {
            ConfigurationSettings = new Observable<NameValueCollectionEditorModel>();
            Name = new Observable<string>();
            OriginalName = new Observable<string>();
        }

        protected override void OnViewLoaded()
        {
            base.OnViewLoaded();

            var configName = QueryParameters.GetValueOrDefault("name", "");

            if (!configName.IsNullOrEmpty())
            {
                BeginLoadConfig(configName);
                OriginalName.Value = configName;
                Name.Value = configName;
            }
            else
            {
                OriginalName.Value = null;
                Name.Value = "";
                EditConfigurationValues(new NameValueCollection());
            }
        }

        private void BeginLoadConfig(string configName)
        {
            ApplicationModel.Current.Client.Config.GetConfig(configName)
                            .ContinueOnUIThread(t =>
                                {
                                    if (t.Exception != null || t.Result == null)
                                    {
                                        ApplicationModel.Current.AddErrorNotification(t.Exception,
                                                                                      string.Format("Configuration '{0}' could not be loaded",configName));
                                        UrlUtil.Navigate("/configuration");
                                    }
                                    else
                                    {
                                        EditConfigurationValues(t.Result);
                                    }
                                });
        }

        private void EditConfigurationValues(NameValueCollection settings)
        {
            hasUnsavedChanges = false;
            var editor = new NameValueCollectionEditorModel(settings);
            editor.Changed += delegate
                {
                    hasUnsavedChanges = true;
                };

            ConfigurationSettings.Value = editor;
        }

        public ICommand SaveCommand
        {
            get { return _saveCommand ?? (_saveCommand = new ActionCommand(HandleSave)); }
        }

        public ICommand DeleteCommand
        {
            get { return _deleteCommand ?? (_deleteCommand = new ActionObservableDependantCommand<string>(OriginalName, HandleDelete, name => !name.IsNullOrEmpty())); }
        }

        public ICommand RefreshCommand
        {
            get { return _refreshCommand ?? (_refreshCommand = new ActionObservableDependantCommand<string>(OriginalName, HandleRefresh, name => !name.IsNullOrEmpty())); }
        }

        private void HandleSave()
        {
            var configurationName = Name.Value;
            var configValues = ConfigurationSettings.Value.GetCurrent();

            if (configurationName.IsNullOrEmpty())
            {
                AskUser.AlertUser("Save Configuration", "Please enter a name for the configuration.");
                return;
            }

            ApplicationModel.Current.AsyncOperations.Do(
               () =>ApplicationModel.Current.Client.Config.SetConfig(configurationName, configValues)
                   .ContinueOnSuccessInTheUIThread(() => OriginalName.Value = configurationName),
            string.Format("Saving configuration '{0}'", configurationName));            
        }

        private void HandleDelete(string configurationName)
        {
            AskUser.ConfirmationAsync(
                "Delete Configuration",
                string.Format("Are you sure you want to delete configuration '{0}'?", configurationName))
                .ContinueWhenTrueInTheUIThread(
                    () =>
                        {
                            ApplicationModel.Current.AsyncOperations.Do(
                                () => ApplicationModel.Current.Client.Config.DeleteConfig(configurationName),
                                string.Format("Deleting configuration '{0}'", configurationName));
                            UrlUtil.Navigate("/Configuration");
                        });
        }

        private void HandleRefresh(string configurationName)
        {
            if (hasUnsavedChanges)
            {
                AskUser.ConfirmationAsync(
                    "Refresh Configuration",
                    string.Format("Are you sure you want to discard your changes to '{0}'?", configurationName))
                       .ContinueWhenTrueInTheUIThread(
                           () => BeginLoadConfig(configurationName));
            }
            else
            {
                BeginLoadConfig(configurationName);
            }
        }
    }
}
