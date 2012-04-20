using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RavenFS.Infrastructure;
using RavenFS.Storage;

namespace RavenFS.Rdc.Wrapper
{
    public class StorageSignatureRepository : ISignatureRepository
    {
        private readonly TransactionalStorage _storage;
        private VolatileSignatureRepository _cacheRepository;
        private readonly string _fileName;

        public StorageSignatureRepository(TransactionalStorage storage, string fileName)
        {
            var tempDirectory = TempDirectoryTools.Create();
            _cacheRepository = new VolatileSignatureRepository(tempDirectory, fileName);
            _storage = storage;
            _fileName = fileName;
        }

        public Stream GetContentForReading(string sigName)
        {
            var ms = new MemoryStream();
            _storage.Batch(
                accessor =>
                {
                    var signatureLevel = GetSignatureLevel(sigName, accessor);
                    if (signatureLevel != null)
                    {
                        accessor.GetSignatureStream(signatureLevel.Id, signatureLevel.Level, stream => stream.CopyTo(ms));
                    }
                    else
                    {
                        throw new FileNotFoundException(sigName + " not found in the repo");
                    }
                });
            ms.Position = 0;
            return ms;
        }

        public Stream CreateContent(string sigName)
        {
            return _cacheRepository.CreateContent(sigName);
        }

        public SignatureInfo GetByName(string sigName)
        {
            SignatureInfo result = null;
            _storage.Batch(
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


        public void Flush(IEnumerable<SignatureInfo> signatureInfos)
        {
            _cacheRepository.Flush(null);
            var fileNames = (from item in signatureInfos
                            group item by item.FileName
                            into fileNameNamesGroup
                            select fileNameNamesGroup.Key).ToList();
            if (fileNames.Count > 1)
            {
                throw new ArgumentException("All SignatureInfo should belong to the same file", "signatureInfos");
            }

            if (fileNames.Count == 0)
            {
                throw new ArgumentException("Must have at least one signature info", "signatureInfos");
            }
            var fileName = fileNames.First();
                             
            _storage.Batch(
                accessor =>
                {
                    accessor.ClearSignatures(fileName);
                    var level = 0;
                    foreach (var item in signatureInfos)
                    {
                        var item1 = item;
                        accessor.AddSignature(fileName, level,
                                              stream =>
                                                  {
                                                      using (var cachedSigContent =
                                                          _cacheRepository.GetContentForReading(item1.Name))
                                                      {
                                                          cachedSigContent.CopyTo(stream);
                                                      }
                                                  });
                        level++;
                    }
                });
            DisposeCacheRepository();
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
            _storage.Batch(
                accessor =>
                {
                    result = (from item in accessor.GetSignatures(_fileName)
                              select new SignatureInfo(item.Level, _fileName)
                                         {
                                             Length = accessor.GetSignatureSize(item.Id, item.Level)
                                         }).ToList();
                });
            if (result.Count() < 1)
            {
                throw new FileNotFoundException("Cannot find signatures for " + _fileName);
            }
            return result;
        }

        public void Clean()
        {
            _storage.Batch(accessor => accessor.ClearSignatures(_fileName));
        }

        public DateTime? GetLastUpdate()
        {
        	SignatureLevels firstOrDefault = null;
			_storage.Batch(accessor =>
			{
				firstOrDefault = accessor.GetSignatures(_fileName).FirstOrDefault();
			});

			if (firstOrDefault == null)
				return null;
        	return firstOrDefault.CreatedAt;
        }

        private void DisposeCacheRepository()
        {
            if (_cacheRepository != null)
            {
                _cacheRepository.Dispose();
                _cacheRepository = null;
            }
        }

        private static SignatureInfo ExtractFileNameAndLevel(string sigName)
        {
            return SignatureInfo.Parse(sigName);
        }

        public void Dispose()
        {
            DisposeCacheRepository();
        }
    }
}