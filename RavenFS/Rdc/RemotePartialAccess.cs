using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public void CopyTo(Stream target, long from, long length)
        {
            _ravenFileSystemClient.DownloadAsync("/rdc/files/", _fileName, target, from, from + length - 1).Wait();
        }
    }
}