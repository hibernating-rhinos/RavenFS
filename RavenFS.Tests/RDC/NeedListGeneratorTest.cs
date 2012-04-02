using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using RavenFS.Infrastructure;
using RavenFS.Rdc.Utils.IO;
using RavenFS.Tests;
using RavenFS.Util;
using Xunit;


namespace RavenFS.Rdc.Wrapper.Test
{
    public class NeedListGeneratorTest
    {
        private readonly ISignatureRepository _signatureRepository = new VolatileSignatureRepository(TempDirectoryTools.Create());

    	private static RandomlyModifiedStream GetSeedStream()
    	{
			return new RandomlyModifiedStream(GetSourceStream(), 0.01, 1);
    	}

    	private static RandomStream GetSourceStream()
    	{
    		return new RandomStream(15*1024*1024, 1);
    	}

    	[MtaFact]
        public void ctor_and_dispose()
        {
            using (var tested = new NeedListGenerator(_signatureRepository, _signatureRepository))
            {
                Assert.NotNull(tested);
            }
        }

        [MtaFact]
        public void Generate_check()
        {
        	IList<SignatureInfo> sourceSignatureInfos;
        	IList<SignatureInfo> seedSignatureInfos;
            using (var generator = new SigGenerator(_signatureRepository))
        	{
        		seedSignatureInfos = generator.GenerateSignatures(GetSeedStream(), "test");
        	}
			var sourceStream = GetSourceStream();
			using (var generator = new SigGenerator(_signatureRepository))
        	{
        		sourceSignatureInfos = generator.GenerateSignatures(sourceStream, "test");
        	}
			var sourceSize = sourceStream.Length;
        	using (var tested = new NeedListGenerator(_signatureRepository, _signatureRepository))
        	{
        		var result = tested.CreateNeedsList(seedSignatureInfos.Last(), sourceSignatureInfos.Last());
        		Assert.NotNull(result);

        		Assert.Equal(0, sourceSize - result.Sum(x => Convert.ToInt32(x.BlockLength)));
        	}
        }

        [MtaFact]
        public void Synchronize_file_with_different_beginning()
        {
            const int size = 5000;
            var differenceChunk = new MemoryStream();
            var sw = new StreamWriter(differenceChunk);

            sw.Write("Coconut is Stupid");
            sw.Flush();

            var sourceContent = PrepareSourceStream(size);
            sourceContent.Position = 0;
            var seedContent = new CombinedStream(differenceChunk, sourceContent);

            IList<SignatureInfo> sourceSignatureInfos;
            IList<SignatureInfo> seedSignatureInfos;
            using (var generator = new SigGenerator(_signatureRepository))
            {
                seedContent.Seek(0, SeekOrigin.Begin);
                seedSignatureInfos = generator.GenerateSignatures(seedContent, "test1");
            }
            using (var generator = new SigGenerator(_signatureRepository))
            {
                sourceContent.Seek(0, SeekOrigin.Begin);
                sourceSignatureInfos = generator.GenerateSignatures(sourceContent, "test2");
            }
            var sourceSize = sourceContent.Length;

            using (var tested = new NeedListGenerator(_signatureRepository, _signatureRepository))
            {
                var result = tested.CreateNeedsList(seedSignatureInfos.Last(), sourceSignatureInfos.Last());
                Assert.NotNull(result);
                Assert.Equal(2, result.Count);
                Assert.Equal(0, sourceSize - result.Sum(x => Convert.ToInt32(x.BlockLength)));
            }
        }

        private static MemoryStream PrepareSourceStream(int lines)
        {
            var ms = new MemoryStream();
            var writer = new StreamWriter(ms);

            for (var i = 1; i <= lines; i++)
            {
                for (var j = 0; j < 100; j++)
                {
                    writer.Write(i.ToString("D4"));
                }
                writer.Write("\n");
            }
            writer.Flush();

            return ms;
        }
    }
}
