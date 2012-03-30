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
        // TODO: this method has bad name
        void AssingToFileName(IEnumerable<SignatureInfo> signatureInfos);
        IEnumerable<SignatureInfo> GetByFileName(string fileName);

        void Clean(string fileName);

        DateTime? GetLastUpdate(string fileName);
    }
}
