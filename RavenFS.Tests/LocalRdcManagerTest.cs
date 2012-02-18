using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Moq;
using RavenFS.Client;
using RavenFS.Storage;
using Rdc.Wrapper;
using Xunit;
using RavenFS.Rdc;

namespace RavenFS.Tests
{
    public class LocalRdcManagerTest
    {
        /*
        [Fact]
        public void Run_sig_generation_if_there_is_no_sig_files_in_repo()
        {
            var rdcAccessMock = new Mock<IRdcAccess>(MockBehavior.Strict);
            rdcAccessMock.Setup(x => x.PrepareSignaturesAsync("test.txt"))
                .Returns(() =>
                             {
                                 var result1 = new Task<SignatureManifest>(() => new SignatureManifest());
                                 result1.Start();
                                 return result1;
                             });

            var signatureRepositoryMock = new Mock<ISignatureRepository>(MockBehavior.Strict);
            signatureRepositoryMock.Setup(x => x.GetByFileName("test.txt"))
                .Returns(new SignatureInfo[] {new SignatureInfo("sigA"), new SignatureInfo("sigB")});
            signatureRepositoryMock.Setup(x => x.GetLastUpdate("test.txt"))
                .Returns(DateTime.Now.AddDays(-1));
            var transactionalStorage = new Mock<TransactionalStorage>(MockBehavior.Strict);
            transactionalStorage.Setup(x => x.Batch(It.IsAny<Action<StorageActionsAccessor>>()))
                .Callback(() =>
                              {
                                  
                              }
                );


            var tested = new LocalRdcManager(signatureRepositoryMock.Object, );
            var fileInfo = new DataInfo()
                               {
                                   CreatedAt = DateTime.Now,
                                   Name = "test.txt"
                               };
            var result = tested.GetSignatureManifest(fileInfo);
            Assert.Equal(2, result.Signatures.Count);
            Assert.Equal("sigA", result.Signatures[0].Name);
            Assert.Equal("sigB", result.Signatures[1].Name);
            Assert.Equal("test.txt", result.FileName);
        }
         * */
    }
}
