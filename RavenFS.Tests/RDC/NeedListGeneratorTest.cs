using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

using RavenFS.Rdc.Utils.IO;
using RavenFS.Tests;
using Xunit;


namespace RavenFS.Rdc.Wrapper.Test
{
    public class NeedListGeneratorTest
    {
        private readonly ISignatureRepository _signatureRepository = new SimpleSignatureRepository();

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
        	long sourceSize;
        	using (var generator = new SigGenerator(_signatureRepository))
        	{
        		seedSignatureInfos = generator.GenerateSignatures(GetSeedStream());
        	}
			var sourceStream = GetSourceStream();
			using (var generator = new SigGenerator(_signatureRepository))
        	{
        		sourceSignatureInfos = generator.GenerateSignatures(sourceStream);
        	}
			sourceSize = sourceStream.Length;
        	using (var tested = new NeedListGenerator(_signatureRepository, _signatureRepository))
        	{
        		var result = tested.CreateNeedsList(seedSignatureInfos.Last(), sourceSignatureInfos.Last());
        		Assert.NotNull(result);

        		Assert.Equal(0, sourceSize - result.Sum(x => Convert.ToInt32(x.BlockLength)));
        	}
        }
    }
}
