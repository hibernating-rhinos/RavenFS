namespace RavenFS.Synchronization.Rdc.Wrapper
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using NLog;
	using RavenFS.Infrastructure;

	public class VolatileSignatureRepository : ISignatureRepository
    {
        private readonly string _fileName;
        private static readonly Logger log = LogManager.GetCurrentClassLogger();
        private readonly string _tempDirectory;
        private IDictionary<string, FileStream> _createdFiles;

        public VolatileSignatureRepository(string fileName)
        {
            _tempDirectory = TempDirectoryTools.Create();
            _fileName = fileName;
            _createdFiles = new Dictionary<string, FileStream>();
        }

        public Stream GetContentForReading(string sigName)
        {
            Flush(null);
            return File.OpenRead(NameToPath(sigName));
        }

        public Stream CreateContent(string sigName)
        {
            var sigFileName = NameToPath(sigName);
            var result = File.Create(sigFileName, 64 * 1024);
            log.Info("File {0} created", sigFileName);
            _createdFiles.Add(sigFileName, result);
            return result;
        }

        public void Flush(IEnumerable<SignatureInfo> signatureInfos)
        {
            CloseCreatedStreams();
        }

        public IEnumerable<SignatureInfo> GetByFileName()
        {
            return from item in GetSigFileNamesByFileName()
                   select SignatureInfo.Parse(item);
        }

        public DateTime? GetLastUpdate()
        {
            var preResult = from item in GetSigFileNamesByFileName()
                            let lastWriteTime = new FileInfo(item).LastWriteTime
                            orderby lastWriteTime descending
                            select lastWriteTime;
            if (preResult.Count() > 0)
            {
                return preResult.First();
            }
            return null;
        }

        private IEnumerable<string> GetSigFileNamesByFileName()
        {
            return Directory.GetFiles(_tempDirectory, _fileName + "*.sig");
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
