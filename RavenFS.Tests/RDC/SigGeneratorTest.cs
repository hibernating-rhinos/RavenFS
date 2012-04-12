using System;
using System.IO;
using RavenFS.Infrastructure;
using RavenFS.Rdc.Utils.IO;
using RavenFS.Tests;
using Xunit;

namespace RavenFS.Rdc.Wrapper.Test
{
    public class SigGeneratorTest : IDisposable
    {
        private ISignatureRepository _signatureRepository;
        private readonly Stream _stream = new MemoryStream();

		public SigGeneratorTest()
		{
			TestDataGenerators.WriteNumbers(_stream, 10000);
			_stream.Position = 0;
		    _signatureRepository = new VolatileSignatureRepository(TempDirectoryTools.Create());
		}

    	[MtaFact]
        public void Ctor_and_dispose()
        {
            using (var tested = new SigGenerator(_signatureRepository))
            {
                Assert.NotNull(tested);
            }
        }

        [MtaFact]
        public void Generate_check()
        {
            using (var rested = new SigGenerator(_signatureRepository))
            {
                var result = rested.GenerateSignatures(_stream, "test");
                Assert.Equal(2, result.Count);
                using (var content = _signatureRepository.GetContentForReading(result[0].Name))
                {
                    Assert.Equal("91b64180c75ef27213398979cc20bfb7", content.GetMD5Hash());
                }
                using (var content = _signatureRepository.GetContentForReading(result[1].Name))
                {
                    Assert.Equal("9fe9d408aed35769e25ece3a56f2d12f", content.GetMD5Hash());
                }
            }
        }

        public void Dispose()
        {
            if (_signatureRepository != null)
            {
                _signatureRepository.Dispose();
                _signatureRepository = null;
            }
        }
    }
}
