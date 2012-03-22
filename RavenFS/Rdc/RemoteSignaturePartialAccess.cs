using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using RavenFS.Client;

namespace RavenFS.Rdc
{
    public class RemoteSignaturePartialAccess : IPartialDataAccess
    {
        private readonly RavenFileSystemClient _ravenFileSystemClient;
        private readonly string _fileName;

        public RemoteSignaturePartialAccess(RavenFileSystemClient ravenFileSystemClient, string fileName)
        {
            _ravenFileSystemClient = ravenFileSystemClient;
            _fileName = fileName;
        }

        public Task CopyToAsync(Stream target, long from, long length)
        {
			return _ravenFileSystemClient.DownloadSignatureAsync(_fileName, target, from, from + length);
        }
    }
}