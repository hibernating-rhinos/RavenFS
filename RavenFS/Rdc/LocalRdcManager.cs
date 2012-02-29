using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using RavenFS.Client;
using RavenFS.Storage;
using RavenFS.Util;
using RavenFS.Rdc.Wrapper;

namespace RavenFS.Rdc
{
    public class LocalRdcManager
    {
        private readonly ISignatureRepository _signatureRepository;
        private readonly TransactionalStorage _transactionalStorage;
        private readonly SigGenerator _sigGenerator;

        public LocalRdcManager(ISignatureRepository signatureRepository, TransactionalStorage transactionalStorage, SigGenerator sigGenerator)
        {
            _signatureRepository = signatureRepository;
            _transactionalStorage = transactionalStorage;
            _sigGenerator = sigGenerator;
        }

        public SignatureManifest GetSignatureManifest(DataInfo dataInfo)
        {
            var lastUpdate = _signatureRepository.GetLastUpdate(dataInfo.Name);
            IEnumerable<SignatureInfo> signatureInfos = null;
            if (lastUpdate == null || lastUpdate < dataInfo.CreatedAt)
            {
                signatureInfos = PrepareSignatures(dataInfo.Name);
                _signatureRepository.AssingToFileName(signatureInfos, dataInfo.Name);
            } 
            else
            {
                signatureInfos = _signatureRepository.GetByFileName(dataInfo.Name);
            }

            var result = new SignatureManifest
                             {
                                 FileLength = dataInfo.Length,
                                 FileName = dataInfo.Name,
                                 Signatures = SignatureInfosToSignatures(signatureInfos)
                             };
            return result;
        }

        public Stream GetSignatureContentForReading(string sigName)
        {
            return _signatureRepository.GetContentForReading(sigName);
        }
          
        private IEnumerable<SignatureInfo> PrepareSignatures(string filename)
        {
            FileAndPages fileAndPages = null;
            _transactionalStorage.Batch(accessor => fileAndPages = accessor.GetFile(filename, 0, 0));
            var input = StorageStream.Reading(_transactionalStorage, fileAndPages.Name);
            return _sigGenerator.GenerateSignatures(input);

        }

        private static IList<Signature> SignatureInfosToSignatures(IEnumerable<SignatureInfo> signatureInfos)
        {
            var preResult = from item in signatureInfos
                            select new Signature
                                       {
                                           Length = item.Length,
                                           Name = item.Name
                                       };
            return preResult.ToList();
        }
    }
}
