using System;
using System.Runtime.Serialization;

namespace RavenFS.Client
{
    [Serializable]
    public class SynchronizationException : Exception
    {
        public SynchronizationException(string message) : base(message)
        {
            
        }

		public SynchronizationException(string message, Exception inner)
			: base(message, inner)
		{
		}

#if !SILVERLIGHT
		protected SynchronizationException(
			SerializationInfo info,
			StreamingContext context)
			: this(info.GetString("ExceptionMessage"))
		{
			
		}
#endif
    }
}