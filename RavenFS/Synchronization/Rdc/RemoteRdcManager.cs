namespace RavenFS.Synchronization.Rdc
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using RavenFS.Client;
	using RavenFS.Infrastructure;
	using Wrapper;

	public class RemoteRdcManager
    {
        private readonly ISignatureRepository _localSignatureRepository;
        private readonly ISignatureRepository _remoteCacheSignatureRepository;
        private readonly RavenFileSystemClient _ravenFileSystemClient;

        public RemoteRdcManager(RavenFileSystemClient ravenFileSystemClient, ISignatureRepository localSignatureRepository, ISignatureRepository remoteCacheSignatureRepository)
        {
            _localSignatureRepository = localSignatureRepository;
            _remoteCacheSignatureRepository = remoteCacheSignatureRepository;
            _ravenFileSystemClient = ravenFileSystemClient;
        }

        /// <summary>
        /// Returns signature manifest and synchronizes remote cache sig repository
        /// </summary>
        /// <param name="dataInfo"></param>
        /// <returns></returns>
        public Task<SignatureManifest> SynchronizeSignaturesAsync(DataInfo dataInfo)
        {
        	return _ravenFileSystemClient.Synchronization.GetRdcManifestAsync(dataInfo.Name)
            	.ContinueWith(task =>
            	{
            		var remoteSignatureManifest1 = task.Result;
            		if (remoteSignatureManifest1.Signatures.Count > 0)
            		{
            			return InternalSynchronizeSignaturesAsync(remoteSignatureManifest1)
            				.ContinueWith(task1 =>
            				{
            					task1.AssertNotFaulted();
            					return remoteSignatureManifest1;
            				});
            		}
            		return new CompletedTask<SignatureManifest>(remoteSignatureManifest1);
            	}).Unwrap();
        }

        private Task InternalSynchronizeSignaturesAsync(SignatureManifest remoteSignatureManifest)
        {
        	var sigPairs = PrepareSigPairs(remoteSignatureManifest);

        	var highestSigName = sigPairs.First().Remote;
        	var highestSigContent = _remoteCacheSignatureRepository.CreateContent(highestSigName);
        	return _ravenFileSystemClient.DownloadSignatureAsync(highestSigName, highestSigContent)
				.ContinueWith(task =>
				{
					highestSigContent.Dispose();
					return task;
				}).Unwrap()
				.ContinueWith(task => SynchronizePairAsync(1, sigPairs))
				.Unwrap();
        	
        }

    	private Task SynchronizePairAsync(int index, IList<LocalRemotePair> sigPairs)
    	{
    		if (index >= sigPairs.Count)
    			return new CompletedTask();

    		var curr = sigPairs[index];
    		var prev = sigPairs[index - 1];

    		return SynchronizeAsync(curr.Local, prev.Local, curr.Remote, prev.Remote)
    			.ContinueWith(task =>
    			{
    				task.AssertNotFaulted();

    				return SynchronizePairAsync(index + 1, sigPairs);
    			}).Unwrap();
    	}

    	private class LocalRemotePair
        {
            public string Local { get; set; }
            public string Remote { get; set; }
        }

        private IList<LocalRemotePair> PrepareSigPairs(SignatureManifest signatureManifest)
        {
            var remoteSignatures = signatureManifest.Signatures;
            var localSignatures = _localSignatureRepository.GetByFileName().ToList();

            var length = Math.Min(remoteSignatures.Count, localSignatures.Count);
            var remoteSignatureNames = remoteSignatures.Take(length).Select(item => item.Name).ToList();
            var localSignatureNames = localSignatures.Take(length).Select(item => item.Name).ToList();
            return
                localSignatureNames.Zip(remoteSignatureNames,
                                        (local, remote) => new LocalRemotePair { Local = local, Remote = remote }).ToList();
        }

        private Task SynchronizeAsync(string localSigName, string localSigSigName, string remoteSigName, string remoteSigSigName)
        {
            using (var needListGenerator = new NeedListGenerator(_localSignatureRepository, _remoteCacheSignatureRepository))            
            {
                var source = new RemoteSignaturePartialAccess(_ravenFileSystemClient, remoteSigName);
                var seed = new SignaturePartialAccess(localSigName, _localSignatureRepository);
                var needList = needListGenerator.CreateNeedsList(SignatureInfo.Parse(localSigSigName),
                                                                  SignatureInfo.Parse(remoteSigSigName));
                var output = _remoteCacheSignatureRepository.CreateContent(remoteSigName);
                return NeedListParser.ParseAsync(source, seed, output, needList)
					.ContinueWith( task =>
					{
						output.Dispose();
						return task;
					}).Unwrap();
                
            }
        }
    }
}