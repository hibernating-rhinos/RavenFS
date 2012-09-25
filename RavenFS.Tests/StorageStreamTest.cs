namespace RavenFS.Tests
{
	using System;
	using System.Collections.Specialized;
	using System.IO;
	using System.Linq;
	using RavenFS.Search;
	using Storage;
	using Util;
	using Xunit;

	public class StorageStreamTest : StorageTest
	{
		private static readonly NameValueCollection EmptyETagMetadata = new NameValueCollection
		                                                            	{
		                                                            		{"ETag", "\"" + Guid.Empty + "\""}
		                                                            	};

		private class MockIndexStorage : IndexStorage
		{
			public MockIndexStorage() : base("", null)
			{
			}

			public override void Index(string key, NameValueCollection metadata)
			{
			}
		}

		[Fact]
		public void StorageStream_should_write_to_storage_by_64kB_pages()
		{
			using (var stream = StorageStream.CreatingNewAndWritting(transactionalStorage, new MockIndexStorage(), "file", EmptyETagMetadata))
			{
				var buffer = new byte[StorageConstants.MaxPageSize];

				new Random().NextBytes(buffer);

				stream.Write(buffer,0,32768);
				stream.Write(buffer, 32767, 32768);
				stream.Write(buffer, 0, 1);
			}

			FileAndPages fileAndPages = null;

			transactionalStorage.Batch(accessor => fileAndPages = accessor.GetFile("file", 0, 10));

			Assert.Equal(2, fileAndPages.Pages.Count);
			Assert.Equal(StorageConstants.MaxPageSize, fileAndPages.Pages[0].Size);
			Assert.Equal(1, fileAndPages.Pages[1].Size);
		}

		[Fact]
		public void SynchronizingFileStream_should_write_to_storage_by_64kB_pages()
		{
			using (var stream = SynchronizingFileStream.CreatingOrOpeningAndWritting(transactionalStorage, new MockIndexStorage(), "file", EmptyETagMetadata))
			{
				var buffer = new byte[StorageConstants.MaxPageSize];

				new Random().NextBytes(buffer);

				stream.Write(buffer, 0, 32768);
				stream.Write(buffer, 32767, 32768);
				stream.Write(buffer, 0, 1);

				stream.PreventUploadComplete = false;
			}

			FileAndPages fileAndPages = null;

			transactionalStorage.Batch(accessor => fileAndPages = accessor.GetFile("file", 0, 10));

			Assert.Equal(2, fileAndPages.Pages.Count);
			Assert.Equal(StorageConstants.MaxPageSize, fileAndPages.Pages[0].Size);
			Assert.Equal(1, fileAndPages.Pages[1].Size);
		}

		[Fact]
		public void StorageStream_can_read_overlaping_byte_ranges_from_last_page()
		{
			var buffer = new byte[StorageConstants.MaxPageSize];

			new Random().NextBytes(buffer);

			using (var stream = StorageStream.CreatingNewAndWritting(transactionalStorage, new MockIndexStorage(), "file", EmptyETagMetadata))
			{
				stream.Write(buffer, 0, StorageConstants.MaxPageSize);
			}

			using (var stream = StorageStream.Reading(transactionalStorage, "file"))
			{
				var readBuffer = new byte[10];

				stream.Seek(StorageConstants.MaxPageSize - 10, SeekOrigin.Begin);
				stream.Read(readBuffer, 0, 10); // read last 10 bytes

				var subBuffer = buffer.ToList().Skip(StorageConstants.MaxPageSize - 10).Take(10).ToArray();

				for (int i = 0; i < 10; i++)
				{
					Assert.Equal(subBuffer[i], readBuffer[i]);
				}

				readBuffer = new byte[5];

				stream.Seek(StorageConstants.MaxPageSize - 5, SeekOrigin.Begin);
				stream.Read(readBuffer, 0, 5); // read last 5 bytes - note that they were read last time as well

				subBuffer = buffer.ToList().Skip(StorageConstants.MaxPageSize - 5).Take(5).ToArray();

				for (int i = 0; i < 5; i++)
				{
					Assert.Equal(subBuffer[i], readBuffer[i]);
				}
			}
		}
	}
}