using System;

namespace RavenFS.Studio.Infrastructure
{
    public interface INotifyBusyness
    {
        event EventHandler<EventArgs> IsBusyChanged;
        bool IsBusy { get; }
    }
}
