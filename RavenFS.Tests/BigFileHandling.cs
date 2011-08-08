using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using RavenFS.Util;
using RavenFS.Storage;
using Xunit;

namespace RavenFS.Tests
{
	public class BigFileHandling : ServerTest
	{
		[Fact]
		public void CanUploadOneMbFileAndGetStats()
		{
			var oneMb = new byte[1024 * 1024];
			new Random().NextBytes(oneMb);

			webClient.UploadData("/files/1mb.bin", "PUT", oneMb);

			var downloadData = webClient.DownloadString("/files/");

			var files = JsonConvert.DeserializeObject<List<FileHeader>>(downloadData,new NameValueCollectionJsonConverter());
			Assert.Equal(1, files.Count);
			Assert.Equal(oneMb.Length, files[0].TotalSize);
			Assert.Equal(oneMb.Length, files[0].UploadedSize);
		}

		[Fact]
		public void CanUploadOneMbFile()
		{
			var oneMb = new byte[1024*1024];
			new Random().NextBytes(oneMb);

			webClient.UploadData("/files/1mb.bin", "PUT", oneMb);

			var downloadData = webClient.DownloadData("/files/1mb.bin");

			Assert.Equal(oneMb.Length, downloadData.Length);
			Assert.Equal(oneMb, downloadData);
		}
	}
}