﻿namespace RavenFS.Tests.Synchronization
{
	using System;
	using System.IO;
	using IO;
	using RavenFS.Tests;
	using Xunit;
	using System.Collections.Generic;
	using RavenFS.Extensions;
	using RavenFS.Synchronization.Rdc.Wrapper;

	public class SigGeneratorTest : IDisposable
    {
        private readonly Stream _stream = new MemoryStream();

		public SigGeneratorTest()
		{
			TestDataGenerators.WriteNumbers(_stream, 10000);
			_stream.Position = 0;
		}

    	[MtaFact]
        public void Ctor_and_dispose()
        {
            using (var tested = new SigGenerator())
            {
                Assert.NotNull(tested);
            }
        }

        [MtaFact]
        public void Generate_check()
        {
            using (var signatureRepository = new VolatileSignatureRepository("test"))
            using (var rested = new SigGenerator())
            {
                var result = rested.GenerateSignatures(_stream, "test", signatureRepository);
                Assert.Equal(2, result.Count);
                using (var content = signatureRepository.GetContentForReading(result[0].Name))
                {
                    Assert.Equal("91b64180c75ef27213398979cc20bfb7", content.GetMD5Hash());
                }
                using (var content = signatureRepository.GetContentForReading(result[1].Name))
                {
                    Assert.Equal("9fe9d408aed35769e25ece3a56f2d12f", content.GetMD5Hash());
                }
            }
        }

		[MtaFact]
		public void Should_be_the_same_signatures()
		{
			int size = 1024*1024*5;
			var randomStream = new RandomStream(size);
			byte[] buffer = new byte[size];
			randomStream.Read(buffer, 0, size);
			var stream = new MemoryStream(buffer);

			var firstSigContentHashes = new List<string>();

			using (var signatureRepository = new VolatileSignatureRepository("test"))
			using (var rested = new SigGenerator())
			{
				var result = rested.GenerateSignatures(stream, "test", signatureRepository);

				foreach (var signatureInfo in result)
				{
					using (var content = signatureRepository.GetContentForReading(signatureInfo.Name))
					{
						firstSigContentHashes.Add(content.GetMD5Hash());
					}
				}
			}

			stream.Position = 0;

			var secondSigContentHashes = new List<string>();

			using (var signatureRepository = new VolatileSignatureRepository("test"))
			using (var rested = new SigGenerator())
			{
				var result = rested.GenerateSignatures(stream, "test", signatureRepository);

				foreach (var signatureInfo in result)
				{
					using (var content = signatureRepository.GetContentForReading(signatureInfo.Name))
					{
						secondSigContentHashes.Add(content.GetMD5Hash());
					}
				}
			}

			Assert.Equal(firstSigContentHashes.Count, secondSigContentHashes.Count);

			for (int i = 0; i < firstSigContentHashes.Count; i++)
			{
				Assert.Equal(firstSigContentHashes[i], secondSigContentHashes[i]);
			}
		}

		public void Dispose()
        {
           
        }
    }
}