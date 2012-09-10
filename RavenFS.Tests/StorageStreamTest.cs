namespace RavenFS.Tests
{
	using System;
	using System.Collections.Specialized;
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

	}
}