using System.IO;
using RavenFS.Rdc.Utils.IO;
using Xunit;

namespace RavenFS.Rdc.Wrapper.Test
{
    public class SigGeneratorTest
    {
        private readonly ISignatureRepository _signatureRepository = new SimpleSignatureRepository();

		public SigGeneratorTest()
        {
            using (Stream file = File.Create("test.txt"))
            {
                TestDataGenerators.WriteNumbers(file, 10000);
            }
        }

        [Fact]
        public void Ctor_and_dispose()
        {
            using (var tested = new SigGenerator(_signatureRepository))
            {
                Assert.NotNull(tested);
            }
        }

        [Fact]
        public void Generate_check()
        {
            using (Stream file = File.OpenRead("test.txt"))
            using (var rested = new SigGenerator(_signatureRepository))
            {
                var result = rested.GenerateSignatures(file);
                Assert.Equal(2, result.Count);
                Assert.Equal("91b64180c75ef27213398979cc20bfb7", _signatureRepository.GetContentForReading(result[0].Name).GetMD5Hash());
                Assert.Equal("9fe9d408aed35769e25ece3a56f2d12f", _signatureRepository.GetContentForReading(result[1].Name).GetMD5Hash());
            }
        }
    }
}
