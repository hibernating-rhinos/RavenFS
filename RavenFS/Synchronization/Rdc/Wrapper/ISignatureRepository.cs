namespace RavenFS.Synchronization.Rdc.Wrapper
{
	using System;
	using System.Collections.Generic;
	using System.IO;

	public interface ISignatureRepository : IDisposable
    {
        Stream GetContentForReading(string sigName);        
        Stream CreateContent(string sigName);
        IEnumerable<SignatureInfo> GetByFileName();
        DateTime? GetLastUpdate();
    }
}
