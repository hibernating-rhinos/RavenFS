using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using RavenFS.Client;
using Rdc.Wrapper;

namespace RavenFS.Rdc
{
    public class RemoteRdcManager
    {
        private readonly ISignatureRepository _localSignatureRepository;
        private readonly ISignatureRepository _remoteCacheSignatureRepository;
        private readonly RavenFileSystemClient _ravenFileSystemClient;
        private readonly NeedListParser _needListParser;

        public RemoteRdcManager(RavenFileSystemClient ravenFileSystemClient, ISignatureRepository localSignatureRepository, ISignatureRepository remoteCacheSignatureRepository)
        {
            _localSignatureRepository = localSignatureRepository;
            _remoteCacheSignatureRepository = remoteCacheSignatureRepository;
            _ravenFileSystemClient = ravenFileSystemClient;
            _needListParser = new NeedListParser();
        }



        /// <summary>
        /// Returns signature manifest and synchronizes remote cache sig repository
        /// </summary>
        /// <param name="dataInfo"></param>
        /// <returns></returns>
        public SignatureManifest SynchronizeSignatures(DataInfo dataInfo)
        {
            var remoteSignatureManifest = _ravenFileSystemClient.GetRdcManifestAsync(dataInfo.Name).Result;
            if (remoteSignatureManifest.Signatures.Count > 0)
            {
                InternalSynchronizeSignatures(dataInfo, remoteSignatureManifest);
            }
            return remoteSignatureManifest;
        }

        private void InternalSynchronizeSignatures(DataInfo dataInfo, SignatureManifest remoteSignatureManifest)
        {
            var sigPairs = PrepareSigPairs(dataInfo, remoteSignatureManifest);

            var highestSigName = sigPairs.First().Remote;
            using (var highestSigContent = _remoteCacheSignatureRepository.CreateContent(highestSigName))
            {
                _ravenFileSystemClient.DownloadSignatureAsync(highestSigName, highestSigContent).Wait();
            }
            for (var i = 1; i < sigPairs.Count(); i++)
            {
                var curr = sigPairs[i];
                var prev = sigPairs[i - 1];
                Synchronize(curr.Local, prev.Local, curr.Remote, prev.Remote);
            }
        }

        private class LocalRemotePair
        {
            public string Local { get; set; }
            public string Remote { get; set; }
        }

        private IList<LocalRemotePair> PrepareSigPairs(DataInfo dataInfo, SignatureManifest signatureManifest)
        {
            var remoteSignatures = signatureManifest.Signatures;
            var localSignatures = _localSignatureRepository.GetByFileName(dataInfo.Name).ToList();

            var length = Math.Min(remoteSignatures.Count, localSignatures.Count);
            var remoteSignatureNames = remoteSignatures.Take(length).Select(item => item.Name).ToList();
            var localSignatureNames = localSignatures.Take(length).Select(item => item.Name).ToList();
            return
                localSignatureNames.Zip(remoteSignatureNames,
                                        (local, remote) => new LocalRemotePair { Local = local, Remote = remote }).ToList();
        }

        private void Synchronize(string localSigName, string localSigSigName, string remoteSigName, string remoteSigSigName)
        {
            using (var needListGenerator = new NeedListGenerator(_localSignatureRepository, _remoteCacheSignatureRepository))            
            {
                var source = new RemoteSignaturePartialAccess(_ravenFileSystemClient, remoteSigName);
                var seed = new SignaturePartialAccess(localSigName, _localSignatureRepository);
                var needList = needListGenerator.CreateNeedsList(new SignatureInfo(localSigSigName),
                                                                  new SignatureInfo(remoteSigSigName));
                using (var output = _remoteCacheSignatureRepository.CreateContent(remoteSigName))
                {
                    _needListParser.Parse(source, seed, output, needList);
                }
            }
        }
    }
}