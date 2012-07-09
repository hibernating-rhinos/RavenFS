namespace RavenFS.Tests.RDC
{
	using System;
	using System.Collections.Generic;
	using System.Collections.Specialized;
	using System.IO;
	using System.Net.Http;
	using Extensions;
	using Rdc.Wrapper;
	using Synchronization.Multipart;
	using Synchronization.Rdc.Wrapper;
	using Util;
	using Xunit;

	public class MultipartTests
	{
		[Fact]
		public void Check_creating_and_parsing()
		{
			var random = new Random();

			var sourceStream = PrepareSourceStream(0);
			var destinationStream = PrepareSourceStream(1);
			var expectedStream = new CombinedStream(new MemoryStream());

			var needList = new List<RdcNeed>();

			for (int index = 0, offset = 0; offset < sourceStream.Length; index++)
			{
				int remainingBytes = (int) (sourceStream.Length - offset);
				var randomLength = remainingBytes > 100 ? random.Next(100, remainingBytes) : remainingBytes;
				
				needList.Add(new RdcNeed
				             	{
				             		BlockLength = (ulong) randomLength,
									BlockType = index % 2 == 0 ? RdcNeedType.Source : RdcNeedType.Seed,
									FileOffset = (ulong) offset
				             	});

				expectedStream = new CombinedStream(expectedStream, index % 2 == 0 ? PrepareSourceStream(0, randomLength) : PrepareSourceStream(1, randomLength));
				offset += randomLength;
			}

			var synchronizationRequest = new SynchronizationMultipartRequest(string.Empty, Guid.Empty, string.Empty,
			                                                                 new NameValueCollection(), sourceStream, needList);

			var content = synchronizationRequest.PrepareMultipartContent();

			var multipartProvider = content.ReadAsMultipartAsync().Result;

			Assert.Equal(needList.Count, multipartProvider.Contents.Count);

			var result = new MemoryStream();
			var synchronizationParser = new SynchronizationMultipartProcessor(string.Empty,
			                                                                  multipartProvider.Contents.GetEnumerator(), destinationStream, result);
			synchronizationParser.ProcessAsync().Wait();

			expectedStream.Position = 0;
			result.Position = 0;
			Assert.Equal(expectedStream.GetMD5Hash(), result.GetMD5Hash());
		}

		private static MemoryStream PrepareSourceStream(byte value, int length = 500000)
		{
			var ms = new MemoryStream();
			var writer = new StreamWriter(ms);
			for (var i = 1; i <= length; i++)
			{
				writer.Write(value);
			}
			writer.Flush();
			ms.Position = 0;
			return ms;
		}
	}
}