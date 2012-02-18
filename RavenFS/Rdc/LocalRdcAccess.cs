using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using RavenFS.Client;
using RavenFS.Storage;
using RavenFS.Util;
using Rdc.Wrapper;

namespace RavenFS.Rdc
{
    public class LocalRdcAccess : IRdcAccess
    {
        protected TransactionalStorage Storage { get; set; }
        protected ISignatureRepository FileAccess { get; set; }
        protected SigGenerator SigGenerator { get; set; }

        public LocalRdcAccess(TransactionalStorage storage, ISignatureRepository fileAccess, SigGenerator sigGenerator)
        {
            Storage = storage;
            FileAccess = fileAccess;
            SigGenerator = sigGenerator;
        }

        public Task<RdcStats> GetRdcStatsAsync()
        {
            throw new NotImplementedException();
        }

        public Task<SignatureManifest> GetRdcManifestAsync(string filename)
        {
            FileAndPages fileAndPages = null;
            Storage.Batch(accessor => fileAndPages = accessor.GetFile(filename, 0, 0));
            Storage.Batch(accessor => accessor.ReadFile(fileAndPages.Name));
            var result = new Task<SignatureManifest>(
                () =>
                    {
                        var input = StorageStream.Reading(Storage, fileAndPages.Name);
                        var signatureInfos = SigGenerator.GenerateSignatures(input);
                        var signatures =
                            from item in signatureInfos
                            select
                                new Signature()
                                    {
                                        Length = item.Length,
                                        Name = item.Name
                                    };
                        return
                            new SignatureManifest()
                                {
                                    FileName = fileAndPages.Name,
                                    FileLength = fileAndPages.TotalSize ?? 0,
                                    Signatures = signatures.ToList()
                                };
                    });
            result.Start();
            return result;
        }

        public SignatureInfo GetSignatureInfo(string sigName)
        {
            return new SignatureInfo(sigName);
        }

        public Task GetSignatureContentAsync(string sigName, Stream destination)
        {
            throw new NotImplementedException();
        }

        public Task GetFileContentAsync(string fileName, Stream destination, long from, long length)
        {
            throw new NotImplementedException();
        }
    }
}