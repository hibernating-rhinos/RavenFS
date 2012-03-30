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

        public SignatureInfo GetByName(string sigName)
        {
            var fullPath = NameToPath(sigName);
            var fi = new FileInfo(fullPath);
            var result = SignatureInfo.Parse(sigName);
            result.Length = fi.Length;
            return result;
        }

        // TODO remove memory cache
        public void AssingToFileName(IEnumerable<SignatureInfo> signatureInfos)
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
