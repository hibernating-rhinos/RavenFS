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
        private readonly ISignatureRepository _cacheRepository;
        private readonly TransactionalStorage _storage;

        public StorageSignatureRepository(TransactionalStorage storage)
        {
            var tempDirectory = TempDirectoryTools.Create();
            _cacheRepository = new VolatileSignatureRepository(tempDirectory);
            _storage = storage;
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
            var fileNames = from item in signatureInfos
                            group item by item.FileName
                            into fileNameNamesGroup
                            select fileNameNamesGroup.Key;
            if (fileNames.Count() > 1)
            {
                throw new ArgumentException("All SignatureInfo should belong to the same file", "signatureInfos");
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
            _cacheRepository.Clean(fileName);
        }


        private static SignatureLevels GetSignatureLevel(string sigName, StorageActionsAccessor accessor)
        {
            var fileNameAndLevel = ExtractFileNameAndLevel(sigName);
            var signatureLevels = accessor.GetSignatures(fileNameAndLevel.FileName);
            return signatureLevels.FirstOrDefault(item => item.Level == fileNameAndLevel.Level);
        }

        public IEnumerable<SignatureInfo> GetByFileName(string fileName)
        {
            IList<SignatureInfo> result = null;
            _storage.Batch(
                accessor =>
                {
                    result = (from item in accessor.GetSignatures(fileName)
                              select new SignatureInfo(item.Level, fileName)
                                         {
                                             Length = accessor.GetSignatureSize(item.Id, item.Level)
                                         }).ToList();
                });
            if (result.Count() < 1)
            {
                throw new FileNotFoundException("Cannot find signatures for " + fileName);
            }
            return result;
        }

        public void Clean(string fileName)
        {
            _storage.Batch(accessor => accessor.ClearSignatures(fileName));
        }

        public DateTime? GetLastUpdate(string fileName)
        {
            // TODO API needed to get last SIG files generation
            return null;
        }

        private static SignatureInfo ExtractFileNameAndLevel(string sigName)
        {
            return SignatureInfo.Parse(sigName);
        }
    }
}