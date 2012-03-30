using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using RavenFS.Extensions;

namespace RavenFS.Rdc.Wrapper
{
    public class VolatileSignatureRepository : ISignatureRepository, IDisposable
    {
        private readonly string _baseDirectory;

        public VolatileSignatureRepository(string path)
        {
        	_baseDirectory = path.ToFullPath();
            Directory.CreateDirectory(_baseDirectory);
        }

		public VolatileSignatureRepository()
			: this(AppDomain.CurrentDomain.BaseDirectory)
        {
        }

        public Stream GetContentForReading(string sigName)
        {
            return File.OpenRead(NameToPath(sigName));
        }

        public Stream CreateContent(string sigName)
        {
            return File.Create(NameToPath(sigName));
        }

        public SignatureInfo GetByName(string sigName)
        {
            var fullPath = NameToPath(sigName);
            var fi = new FileInfo(fullPath);
            var result = SignatureInfo.Parse(sigName);
            result.Length = fi.Length;
            return result;
        }

        public void Flush(IEnumerable<SignatureInfo> signatureInfos)
        {
            // nothing to do
        }

        public IEnumerable<SignatureInfo> GetByFileName(string fileName)
        {
            return from item in GetSigFileNamesByFileName(fileName)
                   select SignatureInfo.Parse(item);
        }

        public void Clean(string fileName)
        {
            foreach (var item in GetSigFileNamesByFileName(fileName))
            {
                File.Delete(item);
            }
        }

        public DateTime? GetLastUpdate(string fileName)
        {
            var preResult = from item in GetSigFileNamesByFileName(fileName)
                            let lastWriteTime = new FileInfo(item).LastWriteTime
                            orderby lastWriteTime descending
                            select lastWriteTime;
            if (preResult.Count() > 0)
            {
                return preResult.First();
            }
            return null;
        }

        private IEnumerable<string> GetSigFileNamesByFileName(string fileName)
        {
            return Directory.GetFiles(_baseDirectory, fileName + "*.sig");
        }

        private string NameToPath(string name)
        {
            return Path.GetFullPath(Path.Combine(_baseDirectory, name));
        }

        public void Dispose()
        {
            Directory.Delete(_baseDirectory, true);
        }
    }
}
