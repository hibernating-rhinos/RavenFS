using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using RavenFS.Extensions;

namespace RavenFS.Rdc.Wrapper
{
    public class SimpleSignatureRepository : ISignatureRepository
    {
        private string _baseDirectory;
        private IDictionary<string, IEnumerable<string>> _sigNamesCache = new Dictionary<string, IEnumerable<string>>();
        private IDictionary<string, DateTime> _sigCreationTimes = new Dictionary<string, DateTime>();

        public SimpleSignatureRepository(string path)
        {
        	_baseDirectory = path.ToFullPath();
            if (!Directory.Exists(_baseDirectory))
            {
                Directory.CreateDirectory(_baseDirectory);
            }
        }

		public SimpleSignatureRepository()
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

        public SignatureInfo GetByName(string name)
        {
            var fullPath = NameToPath(name);
            var fi = new FileInfo(fullPath);
            return
                new SignatureInfo
                    {
                        Name = name,
                        Length = fi.Length
                    };
        }

        public void AssingToFileName(IEnumerable<SignatureInfo> signatureInfos, string fileName)
        {
            _sigNamesCache[fileName] = signatureInfos.Select(item => item.Name).ToList();
            _sigCreationTimes[fileName] = DateTime.Now;
        }

        public IEnumerable<SignatureInfo> GetByFileName(string fileName)
        {
            IEnumerable<string> result;
            if (_sigNamesCache.TryGetValue(fileName, out result))
            {
                foreach (var item in result)
                {
                    yield return GetByName(item);
                }
            }
        }

        public DateTime? GetLastUpdate(string fileName)
        {
            DateTime result;
            if (_sigCreationTimes.TryGetValue(fileName, out result))
            {
                return result;
            }
            return null;
        }

        private string NameToPath(string name)
        {
            return Path.GetFullPath(Path.Combine(_baseDirectory, name + ".sig"));
        }
    }
}
