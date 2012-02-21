using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using RavenFS.Util;
using Rdc.Wrapper;

namespace RavenFS.Rdc
{
    public class SignaturePartialAccess : IPartialDataAccess
    {
        private readonly string _sigName;
        private readonly ISignatureRepository _signatureRepository;

        public SignaturePartialAccess(string sigName, ISignatureRepository signatureRepository)
        {
            _sigName = sigName;
            _signatureRepository = signatureRepository;
        }

        public void CopyTo(Stream target, long from, long length)
        {
            new NarrowedStream(_signatureRepository.GetContentForReading(_sigName), from, from + length - 1).CopyTo(target);
        }
    }
}