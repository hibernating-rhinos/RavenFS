using System;

namespace RavenFS.Client
{
    [Serializable]
    public class SynchronizationException : Exception
    {
        public SynchronizationException(string message) : base(message)
        {
            
        }
    }
}