using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using RavenFS.Storage;
using RavenFS.Util;

namespace RavenFS.Rdc
{
    public class StoragePartialAccess : IPartialDataAccess
    {
        private readonly StorageStream _stream;

        public StoragePartialAccess(TransactionalStorage transactionalStorage, string fileName)
        {
            _stream = StorageStream.Reading(transactionalStorage, fileName);
        }

        public Task CopyToAsync(Stream target, long from, long length)
        {            
            return new NarrowedStream(_stream, from, from + length - 1).CopyToAsync(target);
        }
    }
}