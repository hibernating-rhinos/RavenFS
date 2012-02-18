using System;
using System.Collections.Generic;
using System.Linq;
using RavenFS.Client;
using Rdc.Wrapper;

namespace RavenFS.Rdc
{
    public class LocalRdcManager
    {
        private readonly ISignatureRepository _signatureRepository;
        private readonly IRdcAccess _localRdcAccess;

        public LocalRdcManager(ISignatureRepository signatureRepository, IRdcAccess localRdcAccess)
        {
            _signatureRepository = signatureRepository;
            _localRdcAccess = localRdcAccess;
        }

        public SignatureManifest GetSignatureManifest(DataInfo dataInfo)
        {
            var lastUpdate = _signatureRepository.GetLastUpdate(dataInfo.Name);
            if (lastUpdate == null || lastUpdate < dataInfo.CreatedAt)
            {
                _localRdcAccess.PrepareSignaturesAsync(dataInfo.Name).Wait();
            }

            var signatureInfos = _signatureRepository.GetByFileName(dataInfo.Name);
            var result = new SignatureManifest
                             {
                                 FileLength = dataInfo.Length,
                                 FileName = dataInfo.Name,
                                 Signatures = SignatureInfosToSignatures(signatureInfos)
                             };
            return result;
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
