using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using RavenFS.Rdc.Utils.IO;
using RavenFS.Rdc.Wrapper;
using NUnit;
using NUnit.Framework;


namespace RavenFS.Rdc.Wrapper.Test
{
    [TestFixture]
    public class NeedListGeneratorTest
    {
        private readonly ISignatureRepository _signatureRepository = new SimpleSignatureRepository();

        [TestFixtureSetUp]
        public void Init()
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

        [Test]
        public void ctor_and_dispose()
        {
            using (var tested = new NeedListGenerator(_signatureRepository, _signatureRepository))
            {
                Assert.IsNotNull(tested);
            }
        }

        [Test]
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
                Assert.IsNotNull(result);

                Assert.AreEqual(0, sourceSize - result.Sum(x => Convert.ToInt32(x.BlockLength)));
            }
        }
    }
}
