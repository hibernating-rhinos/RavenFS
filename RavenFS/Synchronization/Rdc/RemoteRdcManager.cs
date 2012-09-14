namespace RavenFS.Synchronization.Rdc
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using RavenFS.Client;
	using Wrapper;

	public class RemoteRdcManager
    {
		private class LocalRemotePair
		{
			public string Local { get; set; }
			public string Remote { get; set; }
		}

        private readonly ISignatureRepository localSignatureRepository;
        private readonly ISignatureRepository remoteCacheSignatureRepository;
        private readonly RavenFileSystemClient ravenFileSystemClient;

        public RemoteRdcManager(RavenFileSystemClient ravenFileSystemClient, ISignatureRepository localSignatureRepository, ISignatureRepository remoteCacheSignatureRepository)
        {
            this.localSignatureRepository = localSignatureRepository;
            this.remoteCacheSignatureRepository = remoteCacheSignatureRepository;
            this.ravenFileSystemClient = ravenFileSystemClient;
        }

        /// <summary>
        /// Returns signature manifest and synchronizes remote cache sig repository
        /// </summary>
        /// <param name="dataInfo"></param>
        /// <returns></returns>
        public async Task<SignatureManifest> SynchronizeSignaturesAsync(DataInfo dataInfo)
        {
	        var remoteSignatureManifest = await ravenFileSystemClient.Synchronization.GetRdcManifestAsync(dataInfo.Name);

			if (remoteSignatureManifest.Signatures.Count > 0)
			{
				var sigPairs = PrepareSigPairs(remoteSignatureManifest);

				var highestSigName = sigPairs.First().Remote;
				using (var highestSigContent = remoteCacheSignatureRepository.CreateContent(highestSigName))
				{
					await ravenFileSystemClient.DownloadSignatureAsync(highestSigName, highestSigContent);
					await SynchronizePairAsync(sigPairs);
				}	
			}

			return remoteSignatureManifest;
        }

    	private async Task SynchronizePairAsync(IList<LocalRemotePair> sigPairs)
    	{
    		for (int i = 1; i < sigPairs.Count; i++)
    		{
				var curr = sigPairs[i];
				var prev = sigPairs[i - 1];

				await SynchronizeAsync(curr.Local, prev.Local, curr.Remote, prev.Remote);
    		}
    	}

        private IList<LocalRemotePair> PrepareSigPairs(SignatureManifest signatureManifest)
        {
            var remoteSignatures = signatureManifest.Signatures;
            var localSignatures = localSignatureRepository.GetByFileName().ToList();

            var length = Math.Min(remoteSignatures.Count, localSignatures.Count);
            var remoteSignatureNames = remoteSignatures.Skip(remoteSignatures.Count - length).Take(length).Select(item => item.Name).ToList();
			var localSignatureNames = localSignatures.Skip(localSignatures.Count - length).Take(length).Select(item => item.Name).ToList();
	        return
		        localSignatureNames.Zip(remoteSignatureNames,
		                                (local, remote) => new LocalRemotePair {Local = local, Remote = remote}).ToList();
        }

        private async Task SynchronizeAsync(string localSigName, string localSigSigName, string remoteSigName, string remoteSigSigName)
        {
            using (var needListGenerator = new NeedListGenerator(localSignatureRepository, remoteCacheSignatureRepository))            
            {
                var source = new RemoteSignaturePartialAccess(ravenFileSystemClient, remoteSigName);
                var seed = new SignaturePartialAccess(localSigName, localSignatureRepository);
                var needList = needListGenerator.CreateNeedsList(SignatureInfo.Parse(localSigSigName),
                                                                  SignatureInfo.Parse(remoteSigSigName));
                using(var output = remoteCacheSignatureRepository.CreateContent(remoteSigName))
                {
	                await NeedListParser.ParseAsync(source, seed, output, needList);
                }
            }
        }
    }
}