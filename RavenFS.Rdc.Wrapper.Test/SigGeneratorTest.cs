using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using NUnit;
using NUnit.Framework;
using Rdc.Utils.IO;

namespace Rdc.Wrapper.Test
{
    [TestFixture]
    public class SigGeneratorTest
    {
        private readonly ISignatureRepository _signatureRepository = new SimpleSignatureRepository();

        [TestFixtureSetUp]
        public void Initi()
        {
            using (Stream file = File.Create("test.txt"))
            {
                TestDataGenerators.WriteNumbers(file, 10000);
            }
        }

        [Test]
        public void Ctor_and_dispose()
        {
            using (var tested = new SigGenerator(_signatureRepository))
            {
                Assert.IsNotNull(tested);
            }
        }

        [Test]
        public void Generate_check()
        {
            using (Stream file = File.OpenRead("test.txt"))
            using (var rested = new SigGenerator(_signatureRepository))
            {
                var result = rested.GenerateSignatures(file);
                Assert.AreEqual(2, result.Count);
                Assert.AreEqual("91b64180c75ef27213398979cc20bfb7", _signatureRepository.GetContentForReading(result[0].Name).GetMD5Hash());
                Assert.AreEqual("9fe9d408aed35769e25ece3a56f2d12f", _signatureRepository.GetContentForReading(result[1].Name).GetMD5Hash());
            }
        }
    }
}
