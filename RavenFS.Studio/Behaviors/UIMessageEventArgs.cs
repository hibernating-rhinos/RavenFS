using System;

namespace RavenFS.Studio.Behaviors
{
    public class UIMessageEventArgs : EventArgs
    {
        private readonly string message;

        public UIMessageEventArgs(string message)
        {
            this.message = message;
        }

        public string Message
        {
            get { return message; }
        }
    }
}
