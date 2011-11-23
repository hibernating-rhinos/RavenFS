using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using Newtonsoft.Json;
using Raven.Tests.Security.OAuth;
using RavenFS.Util;
using RavenFS.Storage;
using Xunit;
using Xunit.Extensions;
using System.Linq;

namespace RavenFS.Tests
{
	public class BigFileHandling : IisExpressTestClient
	{
		[Theory]
		[InlineData(1024 * 1024)]		// 1 mb
		[InlineData(1024 * 1024 * 2)]	// 2 mb
		[InlineData(1024 * 1024 * 4)]	// 4 mb
		[InlineData(1024 * 1024 * 8)]	// 8 mb
		public void CanHandleBigFiles(int size)
		{
			var buffer = new byte[size];
			new Random().NextBytes(buffer);

			webClient.UploadData("/files/mb.bin", "PUT", buffer);

			var files = JsonConvert.DeserializeObject<List<FileHeader>>(webClient.DownloadString("/files/"), new NameValueCollectionJsonConverter());
			Assert.Equal(1, files.Count);
			Assert.Equal(buffer.Length, files[0].TotalSize);
			Assert.Equal(buffer.Length, files[0].UploadedSize);


			var downloadData = webClient.DownloadData("/files/mb.bin");

			Assert.Equal(buffer.Length, downloadData.Length);
			Assert.Equal(buffer, downloadData);
		}

		[Theory]
		[SizeAndPartition(BaseSize = 1024*1024*2, Sizes = 2, Partitions = 3)]
		public void CanReadPartialFiles(int size, int skip)
		{
			var buffer = new byte[size];
			new Random().NextBytes(buffer);

			webClient.UploadData("/files/mb.bin", "PUT", buffer);

			var files = JsonConvert.DeserializeObject<List<FileHeader>>(webClient.DownloadString("/files/"),
			                                                            new NameValueCollectionJsonConverter());
			Assert.Equal(1, files.Count);
			Assert.Equal(buffer.Length, files[0].TotalSize);
			Assert.Equal(buffer.Length, files[0].UploadedSize);
			var readData = CreateWebRequest("/files/mb.bin")
				.WithRange(skip)
				.MakeRequest()
				.ReadData();

			var expected = buffer.Skip(skip).ToArray();
			Assert.Equal(expected.Length, readData.Length);
			Assert.Equal(expected, readData);
		}

		public class SizeAndPartition : DataAttribute
		{
			public int BaseSize { get; set; }
			public int Sizes { get; set; }
			public int Partitions { get; set; }


			public override IEnumerable<object[]> GetData(MethodInfo methodUnderTest, Type[] parameterTypes)
			{
				for (int i = 0; i < Sizes; i++)
				{
					for (int j = 0; j < Partitions; j++)
					{
						var currentSize = (i+1)*BaseSize;
						yield return new object[] {currentSize, currentSize/(j + 1)};
					}
				}
			}
		}
	}
}