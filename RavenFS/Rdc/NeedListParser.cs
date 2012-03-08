using System;
using System.Collections.Generic;
using System.IO;
using RavenFS.Rdc.Wrapper;

namespace RavenFS.Rdc
{
    public static class NeedListParser
    {
        public static void Parse(IPartialDataAccess source, IPartialDataAccess seed, Stream output, IEnumerable<RdcNeed> needList)
        {
        	foreach (var item in needList)
        	{
        		switch (item.BlockType)
        		{
        			case RdcNeedType.Source:
        				source.CopyTo(output, Convert.ToInt64(item.FileOffset), Convert.ToInt64(item.BlockLength));
        				break;
        			case RdcNeedType.Seed:
        				seed.CopyTo(output, Convert.ToInt64(item.FileOffset), Convert.ToInt64(item.BlockLength));
        				break;
        			default:
        				throw new NotSupportedException();
        		}
        	}
        }
    }
}