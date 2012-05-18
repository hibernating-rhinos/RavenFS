using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using RavenFS.Infrastructure;
using RavenFS.Rdc.Wrapper;
using System.Linq;

namespace RavenFS.Rdc
{

    public class NeedListParser
    {
        private TaskCompletionSource<object> _tcs;

        public static Task ParseAsync(IPartialDataAccess source, IPartialDataAccess seed, Stream output, IEnumerable<RdcNeed> needList)
        {
            var me = new NeedListParser();
            return me.ParseAsyncInternal(source, seed, output, needList);
        }

        private NeedListParser()
        {
        }

        private Task ParseAsyncInternal(IPartialDataAccess source, IPartialDataAccess seed, Stream output,
                                      IEnumerable<RdcNeed> needList)
        {
            _tcs = new TaskCompletionSource<object>();
            var needListNullable = from item in needList select (RdcNeed?)item;
            ParseAsyncInternal(source, seed, output, needListNullable);
            return _tcs.Task;
        }

        private void ParseAsyncInternal(IPartialDataAccess source, IPartialDataAccess seed, Stream output,
                                      IEnumerable<RdcNeed?> needList)
        {
            var itemNullable = needList.FirstOrDefault();
            if (itemNullable == null)
            {
                _tcs.SetResult(null);
            }
            else
            {
                var item = itemNullable.Value;
                var tail = needList.Skip(1);
                var newTask = new Func<Task>(
                    () =>
                    {
                        switch (item.BlockType)
                        {
                            case RdcNeedType.Source:
                                return source.CopyToAsync(output,
                                                          Convert.ToInt64(item.FileOffset),
                                                          Convert.ToInt64(item.BlockLength));
                            case RdcNeedType.Seed:
                                return seed.CopyToAsync(output,
                                                        Convert.ToInt64(item.FileOffset),
                                                        Convert.ToInt64(item.BlockLength));
                            default:
                                throw new NotSupportedException();
                        }
                    });
                newTask().ContinueWith(_ => ParseAsyncInternal(source, seed, output, tail));
            }
        }
    }
}
