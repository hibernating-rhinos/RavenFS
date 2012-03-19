using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace RavenFS.Studio.Infrastructure
{
    public class CloseRequestedEventArgs : EventArgs
    {
        public bool DialogResult { get; private set; }

        public CloseRequestedEventArgs(bool dialogResult)
        {
            DialogResult = dialogResult;
        }
    }

    public class DialogModel : Model
    {
        public event EventHandler<CloseRequestedEventArgs> CloseRequested;

        protected void Close(bool dialogResult)
        {
            OnCloseRequested(new CloseRequestedEventArgs(dialogResult));
        }

        protected void OnCloseRequested(CloseRequestedEventArgs e)
        {
            EventHandler<CloseRequestedEventArgs> handler = CloseRequested;
            if (handler != null) handler(this, e);
        }
    }
}
