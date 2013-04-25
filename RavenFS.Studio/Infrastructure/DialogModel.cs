using System;

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

    public class DialogModel : ViewModel
    {
        public event EventHandler<CloseRequestedEventArgs> CloseRequested;

        protected void Close(bool dialogResult)
        {
            OnCloseRequested(new CloseRequestedEventArgs(dialogResult));
        }

        protected void OnCloseRequested(CloseRequestedEventArgs e)
        {
            var handler = CloseRequested;
            if (handler != null) handler(this, e);
        }
    }
}
