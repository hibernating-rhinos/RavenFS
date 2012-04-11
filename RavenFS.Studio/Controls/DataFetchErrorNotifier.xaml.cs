using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using RavenFS.Studio.Infrastructure;
using RavenFS.Client;

namespace RavenFS.Studio.Controls
{
    public partial class DataFetchErrorNotifier : UserControl
    {
        public static readonly DependencyProperty ErrorSourceProperty =
            DependencyProperty.Register("ErrorSource", typeof (INotifyOnDataFetchErrors), typeof (DataFetchErrorNotifier), new PropertyMetadata(default(INotifyOnDataFetchErrors), HandleSourceChanged));

        private static readonly DependencyProperty ErrorTextProperty =
            DependencyProperty.Register("ErrorText", typeof (string), typeof (DataFetchErrorNotifier), new PropertyMetadata(default(string)));

        private ICommand retryCommand;
        private ICommand copyErrorToClipboard;
        private Exception firstException;

        private static void HandleSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var notifier = d as DataFetchErrorNotifier;

            if (e.OldValue != null)
            {
                (e.OldValue as INotifyOnDataFetchErrors).DataFetchError -= notifier.HandleError;
            }

            if (e.NewValue != null)
            {
                (e.NewValue as INotifyOnDataFetchErrors).DataFetchError += notifier.HandleError;
            }
        }

        private void HandleError(object sender, DataFetchErrorEventArgs e)
        {
            if (firstException == null)
            {
                firstException = e.Error;
                VisualStateManager.GoToState(this, "Error", true);

                if (firstException is AggregateException)
                {
                    ErrorText = (firstException as AggregateException).ExtractSingleInnerException().Message;
                }
                else
                {
                    ErrorText = firstException.Message;
                }
            }
        }

        public INotifyOnDataFetchErrors ErrorSource
        {
            get { return (INotifyOnDataFetchErrors) GetValue(ErrorSourceProperty); }
            set { SetValue(ErrorSourceProperty, value); }
        }

        public string ErrorText
        {
            get { return (string)GetValue(ErrorTextProperty); }
            private set { SetValue(ErrorTextProperty, value); }
        }

        public ICommand Retry { get { return retryCommand ?? (retryCommand = new ActionCommand(HandleRetry)); } }

        public ICommand CopyErrorToClipboard { get { return copyErrorToClipboard ?? (copyErrorToClipboard = new ActionCommand(HandleCopyToClipboard)); } }

        public DataFetchErrorNotifier()
        {
            InitializeComponent();

            VisualStateManager.GoToState(this, "NoError", true);
        }

        private void HandleRetry()
        {
            if (ErrorSource != null)
            {
                ErrorSource.Retry();
                firstException = null;
                VisualStateManager.GoToState(this, "NoError", true);
            }
        }

        private void HandleCopyToClipboard()
        {
            if (firstException != null)
            {
                Clipboard.SetText(firstException.ToString());
            }
        }

    }
}
