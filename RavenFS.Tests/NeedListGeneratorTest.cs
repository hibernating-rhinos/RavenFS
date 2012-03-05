using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using RavenFS.Rdc.Utils.IO;
using RavenFS.Rdc.Wrapper;
using Xunit;


namespace RavenFS.Rdc.Wrapper.Test
{
    public class NeedListGeneratorTest
    {
        private readonly ISignatureRepository _signatureRepository = new SimpleSignatureRepository();

        public NeedListGeneratorTest()
        {

            using (Stream file = File.Create("source.bin"))
            {
                new RandomStream(1024*1024*1024, 1).CopyTo(file);
            }

            using (Stream file = File.Create("seed.bin"))
            {
                new RandomlyModifiedStream(new RandomStream(1024 * 1024 * 1024, 1), 0.01, 1).CopyTo(file);
            }
        }

        [Fact]
        public void ctor_and_dispose()
        {
            using (var tested = new NeedListGenerator(_signatureRepository, _signatureRepository))
            {
                Assert.NotNull(tested);
            }
        }

        [Fact]
        public void Generate_check()
        {
            IList<SignatureInfo> sourceSignatureInfos;
            IList<SignatureInfo> seedSignatureInfos;
            long sourceSize;
            using (Stream file = File.OpenRead("seed.bin"))
            {
                using (var generator = new SigGenerator(_signatureRepository))
                {
                    seedSignatureInfos = generator.GenerateSignatures(file);
                }
            }
            using (Stream file = File.OpenRead("source.bin"))
            {
                using (var generator = new SigGenerator(_signatureRepository))
                {
                    sourceSignatureInfos = generator.GenerateSignatures(file);
                }
                sourceSize = file.Length;
            }
            using (var tested = new NeedListGenerator(_signatureRepository, _signatureRepository))
            {
                var result = tested.CreateNeedsList(seedSignatureInfos.Last(), sourceSignatureInfos.Last());
                Assert.NotNull(result);

                Assert.Equal(0, sourceSize - result.Sum(x => Convert.ToInt32(x.BlockLength)));
            }
        }
    }
}
