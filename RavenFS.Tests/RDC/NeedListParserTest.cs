using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RavenFS.Rdc;
using RavenFS.Rdc.Wrapper;
using Xunit;

namespace RavenFS.Tests.RDC
{
    public class NeedListParserTest
    {
        private class PartialDataAccessMock : IPartialDataAccess
        {
            private readonly string _name;
            private readonly ConcurrentStack<string> _stack;

            public PartialDataAccessMock(string name, ConcurrentStack<string> stack)
            {
                _name = name;
                _stack = stack;
            }

            public Task CopyToAsync(Stream target, long from, long length)
            {
                return Task.Factory.StartNew(
                    () =>
                    {
                        Thread.Sleep(new Random().Next(200));
                        _stack.Push(_name);
                    });
            }
        }

        [Fact]
        public void Should_call_source_and_seed_methods_asynchronously_but_in_the_proper_order()
        {
            var stack = new ConcurrentStack<string>();

            const int needListSize = 100;
            var partialDataAccessSourceMock = new PartialDataAccessMock("source", stack);
            var partialDataAccessSeedMock = new PartialDataAccessMock("seed", stack);
            var result = NeedListParser.ParseAsync(partialDataAccessSourceMock, partialDataAccessSeedMock,
                                                   Stream.Null, GenerateAlternatedNeeds(needListSize));
            result.Wait();
            var calls = stack.Reverse().ToList();
            for (var i = 0; i < needListSize; i++)
            {
                var s = i%2 == 0 ? "seed" : "source";
                Assert.Equal(s, calls[i]);
            }
        }

        private static IEnumerable<RdcNeed> GenerateAlternatedNeeds(int numberOfItems)
        {
            for (int i = 0; i < numberOfItems; i++)
            {
                var need = new RdcNeed();
                need.BlockLength = 10;
                if (i % 2 == 0)
                {
                    need.BlockType = RdcNeedType.Seed;
                }
                else
                {
                    need.BlockType = RdcNeedType.Source;
                }
                yield return need;
            }
        }
    }
}
