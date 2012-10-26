namespace RavenFS.Client
{
	using System;

    public class SynchronizationReport
    {
		public SynchronizationReport()
		{
			
		}

		[Newtonsoft.Json.JsonConstructor]
		public SynchronizationReport(string fileName, Guid fileETag, SynchronizationType type)
	    {
		    FileName = fileName;
			FileETag = fileETag;
			Type = type;
	    }

        public string FileName { get; private set; }
		public Guid FileETag { get; private set; }
        public long BytesTransfered { get; set; }
        public long BytesCopied { get; set; }
        public long NeedListLength { get; set; }
        public Exception Exception { get; set; }
		public SynchronizationType Type { get; private set; }
    }
}
