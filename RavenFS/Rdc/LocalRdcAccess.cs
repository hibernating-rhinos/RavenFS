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
        protected IFileAccess FileAccess { get; set; }
        protected SigGenerator SigGenerator { get; set; }
        private readonly FileAccessTool fileAccessTool;        

        public LocalRdcAccess(FileAccessTool fileAccessTool, TransactionalStorage storage, IFileAccess fileAccess, SigGenerator sigGenerator)
        {
            Storage = storage;
            FileAccess = fileAccess;
            SigGenerator = sigGenerator;
            this.fileAccessTool = fileAccessTool;
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

            // TODO: We need to add some cache logic and create Stream own implementation to access FSFiles
            var fileName = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
            var file = FileAccess.Create(fileName);
            return fileAccessTool.WriteFile(file, fileAndPages.Name, 0, null)
                .ContinueWith(task => file.Close())
                .ContinueWith(
                    task =>
                        {
                            using (var inputFile = FileAccess.OpenRead(fileName))
                            {
                                return SigGenerator.GenerateSignatures(inputFile);
                            }
                        })
                .ContinueWith(
                    task =>
                        {
                            var signatures =
                                from item in task.Result
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
        }

        public SignatureInfo GetSignatureInfo(string sigName)
        {
            return new SignatureInfo(FileAccess, sigName);
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