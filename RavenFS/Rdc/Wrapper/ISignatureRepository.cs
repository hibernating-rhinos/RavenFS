using System;
using System.Collections.Generic;
using System.IO;

namespace RavenFS.Rdc.Wrapper
{
    public interface ISignatureRepository
    {
        Stream GetContentForReading(string sigName);        
        Stream CreateContent(string sigName);       
        SignatureInfo GetByName(string sigName);
        void AssingToFileName(IEnumerable<SignatureInfo> signatureInfos, String fileName);
        IEnumerable<SignatureInfo> GetByFileName(string fileName);

        DateTime? GetLastUpdate(string fileName);
    }
}
