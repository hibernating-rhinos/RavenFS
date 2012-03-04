using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RavenFS.Rdc.Wrapper
{
    public interface ISignatureRepository
    {
        Stream GetContentForReading(string name);        
        Stream CreateContent(string name);       
        SignatureInfo GetByName(string name);
        void AssingToFileName(IEnumerable<SignatureInfo> signatureInfos, String fileName);
        IEnumerable<SignatureInfo> GetByFileName(string fileName);

        /// <summary>
        /// Returns last signature update time.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns>Last signature update time or null if signature wasn't found in the repo.</returns>
        DateTime? GetLastUpdate(string fileName);
    }
}
