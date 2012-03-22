using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using RavenFS.Client;

namespace RavenFS.Rdc
{
    public class RemotePartialAccess : IPartialDataAccess
    {
        private readonly RavenFileSystemClient _ravenFileSystemClient;
        private readonly string _fileName;

        public RemotePartialAccess(string baseUrl, string fileName)
        {
            _ravenFileSystemClient = new RavenFileSystemClient(baseUrl);
            _fileName = fileName;
        }

        public Task CopyToAsync(Stream target, long from, long length)
        {
            return _ravenFileSystemClient.DownloadAsync(_fileName, target, from, from + length);
        }
    }
}