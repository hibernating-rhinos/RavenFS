using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using RavenFS.Extensions;

namespace RavenFS.Rdc.Wrapper
{
    public class VolatileSignatureRepository : ISignatureRepository
    {
        private readonly string _fileName;
        private static readonly Logger log = LogManager.GetCurrentClassLogger();
        private readonly ISet<FileStream> _tochedStreams;

        private readonly string baseDirectory;

        public VolatileSignatureRepository(string path, string fileName)
        {
            _tochedStreams = new HashSet<FileStream>();
            _fileName = fileName;
        	baseDirectory = path.ToFullPath();
            Directory.CreateDirectory(baseDirectory);
        }

        public Stream GetContentForReading(string sigName)
        {
            return File.OpenRead(NameToPath(sigName));
        }

        public Stream CreateContent(string sigName)
        {
            var sifFileName = NameToPath(sigName);
            var file = File.Create(sifFileName, 1024 * 128, FileOptions.Asynchronous);
            log.Info("File {0} created", sifFileName);
            _tochedStreams.Add(file);
            return file;
        }

        public void Flush(IEnumerable<SignatureInfo> signatureInfos)
        {
            foreach (var item in _tochedStreams)
            {
                item.Close();
            }
        }

        public IEnumerable<SignatureInfo> GetByFileName()
        {
            return from item in GetSigFileNamesByFileName()
                   select SignatureInfo.Parse(item);
        }

        public void Clean()
        {
            foreach (var item in GetSigFileNamesByFileName())
            {
                File.Delete(item);
                log.Info("File {0} removed", item);
            }
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
            return Directory.GetFiles(baseDirectory, _fileName + "*.sig");
        }

        private string NameToPath(string name)
        {
            return Path.GetFullPath(Path.Combine(baseDirectory, name));
        }

        public void Dispose()
        {
            foreach (var item in _tochedStreams)
            {
                item.Close();
            }
            Directory.Delete(baseDirectory, true);
            log.Info("Direcotry {0} removed", baseDirectory);
        }
    }
}
