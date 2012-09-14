namespace RavenFS.Tests.Synchronization
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using RavenFS.Synchronization.Rdc.Wrapper;
	using Xunit;
	using RavenFS.Synchronization.Rdc;

	public class NeedListParserTest
    {
        private class PartialDataAccessMock : IPartialDataAccess
        {
            private readonly string _name;
            private readonly Queue<string> _queue;

            public PartialDataAccessMock(string name, Queue<string> queue)
            {
                _name = name;
                _queue = queue;
            }

            public Task CopyToAsync(Stream target, long from, long length)
            {
                return Task.Factory.StartNew(
                    () =>
                    {
                        Thread.Sleep(new Random().Next(200));
                        _queue.Enqueue(_name + length);
                    });
            }
        }

        [Fact]
        public void Should_call_source_and_seed_methods_asynchronously_but_in_the_proper_order()
        {
            var queue = new Queue<string>();

            const int needListSize = 100;
            var partialDataAccessSourceMock = new PartialDataAccessMock(RdcNeedType.Source.ToString(), queue);
			var partialDataAccessSeedMock = new PartialDataAccessMock(RdcNeedType.Seed.ToString(), queue);
        	var needList = GenerateAlternatedNeeds(needListSize).ToList();
            var result = NeedListParser.ParseAsync(partialDataAccessSourceMock, partialDataAccessSeedMock,
                                                   Stream.Null, needList, CancellationToken.None);
            result.Wait();
            var calls = queue.ToList();
            for (var i = 0; i < needListSize; i++)
            {
                Assert.Equal(string.Format("{0}{1}", needList[i].BlockType, needList[i].BlockLength), calls[i]);
            }
        }

        private static IEnumerable<RdcNeed> GenerateAlternatedNeeds(int numberOfItems)
        {
            for (int i = 0; i < numberOfItems; i++)
            {
                var need = new RdcNeed();
                need.BlockLength = (ulong) i;
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
