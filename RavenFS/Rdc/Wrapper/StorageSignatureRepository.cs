using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using RavenFS.Storage;

namespace RavenFS.Rdc.Wrapper
{
    public class StorageSignatureRepository : ISignatureRepository
    {
        private ISignatureRepository _cacheRepository;
        private TransactionalStorage _storage;

        public StorageSignatureRepository(TransactionalStorage storage)
        {
            _cacheRepository = new SimpleSignatureRepository();
            _storage = storage;
        }

        public Stream GetContentForReading(string sigName)
        {
            // TODO: Change to some better stream
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
                    if (signatureLevel != null)
                    {
                        result =
                            new SignatureInfo
                                {
                                    Length = accessor.GetSignatureSize(signatureLevel.Id, signatureLevel.Level),
                                    Name = sigName
                                };

                    }
                    else
                    {
                        throw new FileNotFoundException(sigName + " not found in the repo");
                    }
                });
            return result;
        }


        public void AssingToFileName(IEnumerable<SignatureInfo> signatureInfos, string fileName)
        {
            var level = 0;
            foreach (var item in signatureInfos)
            {
                var level1 = level;
                var item2 = item;
                _storage.Batch(
                    accessor =>
                    {
                        accessor.ClearSignatures(fileName);
                        var item1 = item2;
                        accessor.AddSignature(fileName, level1,
                                              stream =>
                                              {
                                                  var cachedSigContent =
                                                      _cacheRepository.GetContentForReading(item1.Name);
                                                  cachedSigContent.CopyTo(stream);
                                              });
                    });
                level++;
            }
        }


        private static SignatureLevels GetSignatureLevel(string sigName, StorageActionsAccessor accessor)
        {
            var fileNameAndLevel = ExtractFileNameAndLevel(sigName);
            var signatureLevels = accessor.GetSignatures(fileNameAndLevel.Item1);
            return signatureLevels.FirstOrDefault(item => item.Level == fileNameAndLevel.Item2);
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

        public DateTime? GetLastUpdate(string fileName)
        {
            throw new NotImplementedException();
        }

        private static readonly Regex SigFileNamePattern = new Regex(@"^(.*?)\.([0-9])\.sig$");

        private static Tuple<string, int> ExtractFileNameAndLevel(string sigName)
        {
            var matcher = SigFileNamePattern.Match(sigName);
            if (matcher.Success)
            {
                return new Tuple<string, int>(matcher.Groups[1].Value, int.Parse(matcher.Groups[2].Value));
            }
            throw new FormatException("SigName: " + sigName + " is not valid");
        }
    }
}