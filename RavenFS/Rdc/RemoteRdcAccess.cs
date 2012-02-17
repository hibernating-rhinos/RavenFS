using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using RavenFS.Client;
using Rdc.Wrapper;

namespace RavenFS.Rdc
{
    public class RemoteRdcAccess : IRdcAccess
    {
        private readonly RavenFileSystemClient client;

        public RemoteRdcAccess(string baseUrl)
        {
            client = new RavenFileSystemClient(baseUrl);
        }

        public Task<RdcStats> GetRdcStatsAsync()
        {
            return client.GetRdcStatsAsync();
        }

        public Task<SignatureManifest> GetRdcManifestAsync(string fileName)
        {
            return client.GetRdcManifestAsync(fileName);
        }

        public SignatureInfo GetSignatureInfo(string sigName)
        {
            throw new NotSupportedException();
        }

        public Task GetSignatureContentAsync(string sigName, Stream destination)
        {
            return client.DownloadSignatureAsync(sigName, destination);
        }

        public Task GetFileContentAsync(string fileName, Stream destination, long from, long length)
        {
            Contract.Requires(length > 0);
            return client.DownloadAsync("/rdc/files/", fileName, destination, from, from + length - 1);
        }
    }
}