using System.IO;
using RavenFS.Rdc.Utils.IO;
using RavenFS.Tests;
using Xunit;

namespace RavenFS.Rdc.Wrapper.Test
{
    public class SigGeneratorTest
    {
        private readonly ISignatureRepository _signatureRepository = new VolatileSignatureRepository();
		Stream stream = new MemoryStream();
		public SigGeneratorTest()
		{
			TestDataGenerators.WriteNumbers(stream, 10000);
			stream.Position = 0;
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
                var result = rested.GenerateSignatures(stream, "test");
                Assert.Equal(2, result.Count);
                Assert.Equal("91b64180c75ef27213398979cc20bfb7", _signatureRepository.GetContentForReading(result[0].Name).GetMD5Hash());
                Assert.Equal("9fe9d408aed35769e25ece3a56f2d12f", _signatureRepository.GetContentForReading(result[1].Name).GetMD5Hash());
            }
        }
    }
}
