namespace RavenFS.Synchronization.Rdc.Wrapper
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using NLog;
	using RavenFS.Storage;

	public class StorageSignatureRepository : ISignatureRepository
    {
        private static readonly Logger log = LogManager.GetCurrentClassLogger();
        private readonly TransactionalStorage storage;
        private readonly string fileName;
		private bool oldSignaturesCleanupPerformed;

		public StorageSignatureRepository(TransactionalStorage storage, string fileName)
        {
            this.storage = storage;
            this.fileName = fileName;
        }

        public Stream GetContentForReading(string sigName)
        {
	        SignatureStream signatureStream = null;
            storage.Batch(
                accessor =>
                {
                    var signatureLevel = GetSignatureLevel(sigName, accessor);
                    if (signatureLevel != null)
                    {
						signatureStream = new SignatureStream(storage, signatureLevel.Id, signatureLevel.Level);
                    }
                    else
                    {
                        throw new FileNotFoundException(sigName + " not found in the repo");
                    }
                });
			signatureStream.Position = 0;
			return signatureStream;
        }

        public Stream CreateContent(string sigName)
        {
			if(!oldSignaturesCleanupPerformed)
			{
				storage.Batch(accessor => accessor.ClearSignatures(fileName));
				oldSignaturesCleanupPerformed = true;
			}

	        var level = SignatureInfo.Parse(sigName).Level;
	        int? id = null;

	        storage.Batch(
		        accessor =>
		        {
			        id = accessor.CreateSignature(fileName, level);
		        });

	        if (id == null)
				throw new InvalidOperationException("Signature " + sigName + " was not created");

			log.Debug("Signature {0} was created and is ready to create it's content", sigName);

			return new SignatureStream(storage, id.Value, level);
        }

        public SignatureInfo GetByName(string sigName)
        {
            SignatureInfo result = null;
            storage.Batch(
                accessor =>
                {
                    var signatureLevel = GetSignatureLevel(sigName, accessor);
                    if (signatureLevel == null)
                    {
                        throw new FileNotFoundException(sigName + " not found in the repo");
                    }
                    result = SignatureInfo.Parse(sigName);
                    result.Length = accessor.GetSignatureSize(signatureLevel.Id, signatureLevel.Level);

                });
            return result;
        }

        private static SignatureLevels GetSignatureLevel(string sigName, StorageActionsAccessor accessor)
        {
            var fileNameAndLevel = ExtractFileNameAndLevel(sigName);
            var signatureLevels = accessor.GetSignatures(fileNameAndLevel.FileName);
            return signatureLevels.FirstOrDefault(item => item.Level == fileNameAndLevel.Level);
        }

        public IEnumerable<SignatureInfo> GetByFileName()
        {
            IList<SignatureInfo> result = null;
            storage.Batch(
                accessor =>
                {
                    result = (from item in accessor.GetSignatures(fileName)
                              orderby item.Level
                              select new SignatureInfo(item.Level, fileName)
                                         {
                                             Length = accessor.GetSignatureSize(item.Id, item.Level)
                                         }). ToList();
                });

            if (result.Count() < 1)
            {
                throw new FileNotFoundException("Cannot find signatures for " + fileName);
            }
            return result;
        }

        public DateTime? GetLastUpdate()
        {
            SignatureLevels firstOrDefault = null;
            storage.Batch(accessor =>
            {
                firstOrDefault = accessor.GetSignatures(fileName).FirstOrDefault();
            });

            if (firstOrDefault == null)
                return null;
            return firstOrDefault.CreatedAt;
        }

        private static SignatureInfo ExtractFileNameAndLevel(string sigName)
        {
            return SignatureInfo.Parse(sigName);
        }

        public void Dispose()
        {
        }
    }
}