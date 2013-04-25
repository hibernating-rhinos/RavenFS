using System;

namespace RavenFS.Studio.Infrastructure
{
    public enum InterimDataMode
    {
        Clear,
        ShowStaleData
    }
 
    public class VirtualCollectionChangedEventArgs : EventArgs
    {
        public InterimDataMode Mode { get; private set; }

        public VirtualCollectionChangedEventArgs(InterimDataMode mode)
        {
            Mode = mode;
        }
    }
}
