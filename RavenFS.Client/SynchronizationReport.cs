namespace RavenFS.Client
{
	using System;

    public class SynchronizationReport
    {
        public string FileName { get; set; }
        public long BytesTransfered { get; set; }
        public long BytesCopied { get; set; }
        public long NeedListLength { get; set; }
        public Exception Exception { get; set; }
		public SynchronizationType Type { get; set; }
    }
}
