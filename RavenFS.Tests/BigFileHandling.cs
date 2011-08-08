using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using RavenFS.Util;
using RavenFS.Storage;
using Xunit;
using Xunit.Extensions;

namespace RavenFS.Tests
{
	public class BigFileHandling : ServerTest
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

	}
}