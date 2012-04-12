using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using RavenFS.Infrastructure;
using RavenFS.Rdc.Wrapper;

namespace RavenFS.Rdc
{
	public static class NeedListParser
	{
		public static Task ParseAsync(IPartialDataAccess source, IPartialDataAccess seed, Stream output,
		                              IList<RdcNeed> needList)
		{
		    var tcs = new TaskCompletionSource<object>();

            // TODO: This code is causing a Stack Over Flow Exception, not sure how to fix this, so this is a workaround for now

		    Task.Factory.StartNew(() =>
		                              {
		                                  try
		                                  {
                                              foreach (var item in needList)
                                              {
                                                  switch (item.BlockType)
                                                  {
                                                      case RdcNeedType.Source:
                                                          source.CopyToAsync(output, Convert.ToInt64(item.FileOffset),
                                                                             Convert.ToInt64(item.BlockLength)).Wait();
                                                          break;
                                                      case RdcNeedType.Seed:
                                                          seed.CopyToAsync(output, Convert.ToInt64(item.FileOffset),
                                                                           Convert.ToInt64(item.BlockLength)).Wait();
                                                          break;
                                                      default:
                                                          throw new NotSupportedException();
                                                  }
                                              }

		                                      tcs.TrySetResult(null);
		                                  }
		                                  catch (Exception e)
		                                  {
		                                      tcs.TrySetException(e);
		                                  }
		                              });

		    return tcs.Task;
		}
	}
}