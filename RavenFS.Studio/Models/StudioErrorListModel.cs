using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Raven.Studio.Messages;
using System.Linq;
using RavenFS.Studio.Infrastructure;
using ActionCommand = Microsoft.Expression.Interactivity.Core.ActionCommand;

namespace RavenFS.Studio.Models
{
    public class StudioErrorListModel : DialogModel
    {
        private ICollectionView collectionView;
        private ICommand clear;
        private ICommand copyErrorDetailsToClipboard;
        private Notification selectedItem;
        private ICommand close;

        public ICollectionView Errors
        {
            get
            {

                return collectionView ?? (collectionView = new PagedCollectionView(ApplicationModel.Current.Notifications)
                                            {
                                                Filter = item => ((Notification) item).Level == NotificationLevel.Error
                                            });
            }
        }

        public Notification SelectedItem
        {
            get { return selectedItem; }
            set
            {
                selectedItem = value;
                OnPropertyChanged(() => SelectedItem);
            }
        }

        public ICommand CloseCommand
        {
            get { return close ?? (close = new ActionCommand(() => Close(true))); }
        }

        public ICommand Clear
        {
            get { return clear ?? (clear = new ActionCommand(() => ApplicationModel.Current.Notifications.Clear())); }
        }

        public ICommand CopyErrorDetailsToClipboard
        {
            get
            {
                return copyErrorDetailsToClipboard ??
                       (copyErrorDetailsToClipboard = new ActionCommand(HandleCopyErrorDetailsToClipboard));
            }
        }

        private void HandleCopyErrorDetailsToClipboard(object parameter)
        {
            var notification = parameter as Notification;
            if (notification == null)
            {
                return;
            }

            Clipboard.SetText(notification.Details);
        }

        protected override void OnViewLoaded()
        {
            base.OnViewLoaded();

            if (SelectedItem == null)
            {
                SelectedItem =
                    ApplicationModel.Current.Notifications.LastOrDefault(n => n.Level == NotificationLevel.Error);
            }
        }
    }
}
