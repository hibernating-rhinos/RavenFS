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
