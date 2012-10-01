namespace RavenFS.Synchronization.Rdc.Wrapper
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using NLog;
	using RavenFS.Infrastructure;
	using RavenFS.Storage;

	public class StorageSignatureRepository : ISignatureRepository
    {
        private static readonly Logger log = LogManager.GetCurrentClassLogger();
        private readonly TransactionalStorage _storage;
        private readonly string _fileName;
        private readonly string _tempDirectory;
        private IDictionary<string, FileStream> _createdFiles;

        public StorageSignatureRepository(TransactionalStorage storage, string fileName)
        {
            _tempDirectory = TempDirectoryTools.Create();
            _storage = storage;
            _fileName = fileName;
            _createdFiles = new Dictionary<string, FileStream>();
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
            var sigFileName = NameToPath(sigName);
            var result = File.Create(sigFileName, 64 * 1024);
            log.Info("File {0} created", sigFileName);
            _createdFiles.Add(sigFileName, result);
            return result;
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
            if (_createdFiles.Count == 0)
            {
                throw new ArgumentException("Must have at least one signature info", "signatureInfos");
            }

            CloseCreatedStreams();

            _storage.Batch(
                accessor =>
                {
                    accessor.ClearSignatures(_fileName);
                    foreach (var item in _createdFiles)
                    {
                        var item1 = item;
                        var level = SignatureInfo.Parse(item.Key).Level;
                        accessor.AddSignature(_fileName, level,
                                              stream =>
                                              {
                                                  using (var cachedSigContent = File.OpenRead(item1.Key))
                                                  {
                                                      cachedSigContent.CopyTo(stream);
                                                  }
                                              });
                    }
                });
            _createdFiles = new Dictionary<string, FileStream>();
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
                              orderby item.Level
                              select new SignatureInfo(item.Level, _fileName)
                                         {
                                             Length = accessor.GetSignatureSize(item.Id, item.Level)
                                         }). ToList();
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

        private static SignatureInfo ExtractFileNameAndLevel(string sigName)
        {
            return SignatureInfo.Parse(sigName);
        }

        private string NameToPath(string name)
        {
            return Path.GetFullPath(Path.Combine(_tempDirectory, name));
        }

        private void CloseCreatedStreams()
        {
            foreach (var item in _createdFiles)
            {
                item.Value.Close();
            }
        }

        public void Dispose()
        {
            CloseCreatedStreams();
            Directory.Delete(_tempDirectory, true);
        }
    }
}