using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using RavenFS.Client;
using Rdc.Wrapper;

namespace RavenFS.Rdc
{
    public interface IRdcAccess
    {
        Task<RdcStats> GetRdcStatsAsync();
        Task<SignatureManifest> PrepareSignaturesAsync(string fileName);
        SignatureInfo GetSignatureInfo(string sigName);
        Task GetSignatureContentAsync(string sigName, Stream destination);
        Task GetFileContentAsync(string fileName, Stream destination, long from, long length);
    }
}