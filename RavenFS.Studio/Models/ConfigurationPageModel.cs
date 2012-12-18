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
using RavenFS.Studio.Behaviors;
using RavenFS.Studio.Commands;
using RavenFS.Studio.Extensions;
using RavenFS.Studio.Features.Configuration;
using RavenFS.Studio.Infrastructure;
using RavenFS.Studio.Infrastructure.Input;

namespace RavenFS.Studio.Models
{
    public class ConfigurationPageModel : PageModel
    {
        public static string SearchStartedMessage = "SearchStarted";

        private ICommand addNewConfiguration;
        private ICommand deleteSelectedItemsCommand;
        private ICommand editConfiguration;
        private ICommand clearSearchCommand;
        private ICommand showSearchCommand;

        public VirtualCollection<ConfigurationModel> Configurations { get; private set; }
        public ItemSelection<VirtualItem<ConfigurationModel>> SelectedItems { get; private set; }
        public Observable<string> SearchPattern { get; private set; }
        public Observable<bool> IsSearchVisible { get; private set; }
        public ICommand AddNewConfiguration { get { return addNewConfiguration ?? (addNewConfiguration = new ActionCommand(HandleAddNewConfiguration));  } }

        public ICommand EditConfiguration { get { return editConfiguration ?? (editConfiguration = new ActionCommand(HandleEditConfiguration)); } }

        private void HandleEditConfiguration(object parameter)
        {
            var configuration = parameter as VirtualItem<ConfigurationModel>;
            if (configuration == null || !configuration.IsRealized)
            {
                return;
            }

            UrlUtil.Navigate("/EditConfiguration?name=" + configuration.Item.Name);
        }

        public ICommand ShowSearch
        {
            get
            {
                return showSearchCommand ?? (showSearchCommand = new ActionCommand(() =>
                {
                    IsSearchVisible.Value = true;
                    OnUIMessage(new UIMessageEventArgs(SearchStartedMessage));
                }));
            }
        }

        public ICommand DeleteSelectedItemsCommand
        {
            get { return deleteSelectedItemsCommand ?? (deleteSelectedItemsCommand = new DeleteConfigurationsCommand(SelectedItems)); }
        }

        public ICommand ClearSearch
        {
            get
            {
                return clearSearchCommand ??
                       (clearSearchCommand = new ActionCommand(
                                                 () =>
                                                 {
                                                     SearchPattern.Value = "";
                                                     IsSearchVisible.Value = false;
                                                 }
                                                 ));
            }
        }

        public ConfigurationPageModel()
        {
            var configurationsCollectionSource = new ConfigurationsCollectionSource();
            Configurations = new VirtualCollection<ConfigurationModel>(configurationsCollectionSource, 30, 10);
            SelectedItems = new ItemSelection<VirtualItem<ConfigurationModel>>();
            IsSearchVisible = new Observable<bool>();
            SearchPattern = new Observable<string>() { Value = "" };
            SearchPattern.ObserveChanged().Throttle(TimeSpan.FromSeconds(1)).Subscribe(value => configurationsCollectionSource.Prefix = value);
        }

        private void HandleAddNewConfiguration()
        {
            UrlUtil.Navigate("/EditConfiguration");
        }
            

        protected override void OnViewLoaded()
        {
            Configurations.Refresh(RefreshMode.PermitStaleDataWhilstRefreshing);
            ApplicationModel.Current.Client.Notifications
                .ConfigurationChanges()
                .SampleResponsive(TimeSpan.FromSeconds(1))
                .TakeUntil(Unloaded)
                .ObserveOn(DispatcherScheduler.Instance)
                .Subscribe(_ => Configurations.Refresh(RefreshMode.PermitStaleDataWhilstRefreshing));
        }
    }
}
