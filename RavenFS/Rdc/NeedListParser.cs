using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using RavenFS.Rdc.Wrapper;

namespace RavenFS.Rdc
{
    public static class NeedListParser
    {
        public static Task ParseAsync(IPartialDataAccess source, IPartialDataAccess seed, Stream output, IEnumerable<RdcNeed> needList)
        {
            return Task.Factory.StartNew(() =>
            {
                foreach (var item in needList)
                {
                    switch (item.BlockType)
                    {
                        case RdcNeedType.Source:
                            source.CopyToAsync(output, Convert.ToInt64(item.FileOffset), Convert.ToInt64(item.BlockLength)).Wait();
                            break;
                        case RdcNeedType.Seed:
                            seed.CopyToAsync(output, Convert.ToInt64(item.FileOffset), Convert.ToInt64(item.BlockLength)).Wait();
                            break;
                        default:
                            throw new NotSupportedException();
                    }
                }
            });
        }
    }
}