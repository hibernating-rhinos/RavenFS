using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using RavenFS.Storage;
using RavenFS.Util;

namespace RavenFS.Rdc
{
    public class StoragePartialAccess : IPartialDataAccess
    {
        private readonly TransactionalStorage _transactionalStorage;
        private readonly string _fileName;
        private readonly StorageStream _stream;

        public StoragePartialAccess(TransactionalStorage transactionalStorage, string fileName)
        {
            _transactionalStorage = transactionalStorage;
            _fileName = fileName;
            _stream = StorageStream.Reading(transactionalStorage, fileName);
        }

        public void CopyTo(Stream target, long from, long length)
        {            
            new NarrowedStream(_stream, from, from + length - 1).CopyTo(target);
        }
    }
}