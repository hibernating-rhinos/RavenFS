using System;
using System.Collections.Specialized;
using RavenFS.Extensions;
using RavenFS.Tests.Tools;
using RavenFS.Util;
using Xunit;

namespace RavenFS.Tests
{
	using System.Linq;
	using Storage;

	public class PagesTests : IDisposable
	{
		readonly Storage.TransactionalStorage storage;

		private readonly NameValueCollection metadataWithEtag = new NameValueCollection()
		                                               	{
		                                               		{"ETag", "\"" + Guid.Empty +"\""}
		                                               	};


		public PagesTests()
		{
			IOExtensions.DeleteDirectory("test");
			storage = new Storage.TransactionalStorage("test", metadataWithEtag);
			storage.Initialize();
		}

		[Fact]
		public void CanInsertPage()
		{
			storage.Batch(accessor => accessor.InsertPage(new byte[] { 1, 2, 3, 4, 5, 6 }, 4));
		}

		[Fact]
		public void CanAssociatePageWithFile()
		{
			storage.Batch(accessor =>
			{
				accessor.PutFile("test.csv", 12, metadataWithEtag);

				var hashKey = accessor.InsertPage(new byte[] {1, 2, 3, 4, 5, 6}, 4);
				accessor.AssociatePage("test.csv", hashKey,0, 4);

				hashKey = accessor.InsertPage(new byte[] {5, 6, 7, 8, 9}, 4);
				accessor.AssociatePage("test.csv", hashKey, 1, 4);
			});
		}

		[Fact]
		public void CanReadFilePages()
		{
			storage.Batch(accessor =>
			{
				accessor.PutFile("test.csv", 16, metadataWithEtag);

				var hashKey = accessor.InsertPage(new byte[] { 1, 2, 3, 4, 5, 6 }, 4);
				accessor.AssociatePage("test.csv", hashKey, 0, 4);

				hashKey = accessor.InsertPage(new byte[] { 5, 6, 7, 8, 9 }, 4);
				accessor.AssociatePage("test.csv", hashKey, 1, 4);

				hashKey = accessor.InsertPage(new byte[] { 6, 7, 8, 9 }, 4);
				accessor.AssociatePage("test.csv", hashKey, 2, 4);
			});


			storage.Batch(accessor =>
			{
				var file = accessor.GetFile("test.csv", 0, 2);
				Assert.NotNull(file);
				Assert.Equal(2, file.Pages.Count);
			});
		}

		[Fact]
		public void CanReadFilePages_SecondPage()
		{
			storage.Batch(accessor =>
			{
				accessor.PutFile("test.csv", 16, metadataWithEtag);

				var hashKey = accessor.InsertPage(new byte[] { 1, 2, 3, 4, 5, 6 }, 4);
				accessor.AssociatePage("test.csv", hashKey, 0, 4);

				hashKey = accessor.InsertPage(new byte[] { 5, 6, 7, 8, 9 }, 4);
				accessor.AssociatePage("test.csv", hashKey, 1, 4);

				hashKey = accessor.InsertPage(new byte[] { 6, 7, 8, 9 }, 4);
				accessor.AssociatePage("test.csv", hashKey, 2, 4);
			});


			storage.Batch(accessor =>
			{
				var file = accessor.GetFile("test.csv", 2, 2);
				Assert.NotNull(file);
				Assert.Equal(1, file.Pages.Count);
			});
		}

		[Fact]
		public void CanReadMetadata()
		{
			metadataWithEtag["test"] = "abc";
			storage.Batch(accessor => accessor.PutFile("test.csv", 16, metadataWithEtag));


			storage.Batch(accessor =>
			{
				var file = accessor.GetFile("test.csv", 2, 2);
				Assert.NotNull(file);
				Assert.Equal("abc", file.Metadata["test"]);
			});
		}

		[Fact]
		public void CanReadFileContents()
		{
			storage.Batch(accessor =>
			{
				accessor.PutFile("test.csv", 16, metadataWithEtag);

				var hashKey = accessor.InsertPage(new byte[] { 1, 2, 3, 4, 5, 6 }, 4);
				accessor.AssociatePage("test.csv", hashKey, 0, 4);

				hashKey = accessor.InsertPage(new byte[] { 5, 6, 7, 8, 9 }, 4);
				accessor.AssociatePage("test.csv", hashKey, 1, 4);

				hashKey = accessor.InsertPage(new byte[] { 6, 7, 8, 9 }, 4);
				accessor.AssociatePage("test.csv", hashKey, 2, 4);
			});


			storage.Batch(accessor =>
			{
				var file = accessor.GetFile("test.csv", 0, 4);
				var bytes = new byte[4];

				accessor.ReadPage(file.Pages[0].Id, bytes);
				Assert.Equal(new byte[] { 1, 2, 3, 4 }, bytes);

				accessor.ReadPage(file.Pages[1].Id, bytes);
				Assert.Equal(new byte[] {5, 6, 7, 8}, bytes);
				accessor.ReadPage(file.Pages[2].Id, bytes);
				Assert.Equal(new byte[] {6, 7, 8, 9}, bytes);
			});
		}

		[Fact]
		public void CanInsertAndReadPage()
		{
			int key = 0;
			storage.Batch(accessor =>
			{
				key = accessor.InsertPage(new byte[] { 1, 2, 3, 4, 5, 6 }, 4);
			});

			storage.Batch(accessor =>
			{
				var buffer = new byte[4];
				Assert.Equal(4, accessor.ReadPage(key, buffer));
				Assert.Equal(new byte[] { 1, 2, 3, 4 }, buffer);
			});
		}

		[Fact]
		public void CanGetPageRangeContainingBytes()
		{
			storage.Batch(accessor =>
			{
				accessor.PutFile("file", 16, metadataWithEtag);

				var hashKey = accessor.InsertPage(new byte[] { 1, 2, 3, 4 }, 4);
				accessor.AssociatePage("file", hashKey, 0, 4);

				hashKey = accessor.InsertPage(new byte[] { 5, 6, 7 }, 3);
				accessor.AssociatePage("file", hashKey, 1, 3);

				hashKey = accessor.InsertPage(new byte[] { 8, 9, 10, 11 }, 4);
				accessor.AssociatePage("file", hashKey, 2, 4);

				hashKey = accessor.InsertPage(new byte[] { 12, 13, 14, 15 }, 4);
				accessor.AssociatePage("file", hashKey, 3, 4);
			});

			PageRange pageRange = null;

			storage.Batch(accessor => pageRange = accessor.GetPageRangeContainingBytes("file", 0, 3));

			Assert.Equal(1, pageRange.OrderedPages.Count);
			Assert.Equal(1, pageRange.OrderedPages.First().Id);
			Assert.Equal(1, pageRange.OrderedPages.Last().Id);
			Assert.Equal(0L, pageRange.StartByte);
			Assert.Equal(3L, pageRange.EndByte);

			storage.Batch(accessor => pageRange = accessor.GetPageRangeContainingBytes("file", 2, 3));

			Assert.Equal(1, pageRange.OrderedPages.Count);
			Assert.Equal(1, pageRange.OrderedPages.First().Id);
			Assert.Equal(1, pageRange.OrderedPages.Last().Id);
			Assert.Equal(0L, pageRange.StartByte);
			Assert.Equal(3L, pageRange.EndByte);

			storage.Batch(accessor => pageRange = accessor.GetPageRangeContainingBytes("file", 3, 4));

			Assert.Equal(2, pageRange.OrderedPages.Count);
			Assert.Equal(1, pageRange.OrderedPages.First().Id);
			Assert.Equal(2, pageRange.OrderedPages.Last().Id);
			Assert.Equal(0L, pageRange.StartByte);
			Assert.Equal(6L, pageRange.EndByte);

			storage.Batch(accessor => pageRange = accessor.GetPageRangeContainingBytes("file", 3, 5));

			Assert.Equal(2, pageRange.OrderedPages.Count);
			Assert.Equal(1, pageRange.OrderedPages.First().Id);
			Assert.Equal(2, pageRange.OrderedPages.Last().Id);
			Assert.Equal(0L, pageRange.StartByte);
			Assert.Equal(6L, pageRange.EndByte);

			storage.Batch(accessor => pageRange = accessor.GetPageRangeContainingBytes("file", 5, 9));

			Assert.Equal(2, pageRange.OrderedPages.Count);
			Assert.Equal(2, pageRange.OrderedPages.First().Id);
			Assert.Equal(3, pageRange.OrderedPages.Last().Id);
			Assert.Equal(4L, pageRange.StartByte);
			Assert.Equal(10L, pageRange.EndByte);

			storage.Batch(accessor => pageRange = accessor.GetPageRangeContainingBytes("file", 1, 12));

			Assert.Equal(4, pageRange.OrderedPages.Count);
			Assert.Equal(1, pageRange.OrderedPages.First().Id);
			Assert.Equal(4, pageRange.OrderedPages.Last().Id);
			Assert.Equal(0L, pageRange.StartByte);
			Assert.Equal(14L, pageRange.EndByte);
		}

		[Fact]
		public void CanGetPageRangeBetweenBytes()
		{
			storage.Batch(accessor =>
			{
				accessor.PutFile("file", 16, metadataWithEtag);

				var hashKey = accessor.InsertPage(new byte[] {1, 2, 3, 4}, 4);
				accessor.AssociatePage("file", hashKey, 0, 4);

				hashKey = accessor.InsertPage(new byte[] {5, 6, 7}, 3);
				accessor.AssociatePage("file", hashKey, 1, 3);

				hashKey = accessor.InsertPage(new byte[] {8, 9, 10, 11}, 4);
				accessor.AssociatePage("file", hashKey, 2, 4);

				hashKey = accessor.InsertPage(new byte[] {12, 13, 14, 15}, 4);
				accessor.AssociatePage("file", hashKey, 3, 4);
			});

			PageRange pageRange = null;

			storage.Batch(accessor => pageRange = accessor.GetPageRangeBetweenBytes("file", 1, 3));

			Assert.Null(pageRange);

			storage.Batch(accessor => pageRange = accessor.GetPageRangeBetweenBytes("file", 6, 7));

			Assert.Null(pageRange);

			storage.Batch(accessor => pageRange = accessor.GetPageRangeBetweenBytes("file", 11, 13));

			Assert.Null(pageRange);

			storage.Batch(accessor => pageRange = accessor.GetPageRangeBetweenBytes("file", 13, 14));

			Assert.Null(pageRange);

			storage.Batch(accessor => pageRange = accessor.GetPageRangeBetweenBytes("file", 3, 8));

			Assert.Equal(1, pageRange.OrderedPages.Count);
			Assert.Equal(2, pageRange.OrderedPages.First().Id);
			Assert.Equal(2, pageRange.OrderedPages.Last().Id);
			Assert.Equal(4L, pageRange.StartByte);
			Assert.Equal(6L, pageRange.EndByte);

			storage.Batch(accessor => pageRange = accessor.GetPageRangeBetweenBytes("file", 3, 11));

			Assert.Equal(2, pageRange.OrderedPages.Count);
			Assert.Equal(2, pageRange.OrderedPages.First().Id);
			Assert.Equal(3, pageRange.OrderedPages.Last().Id);
			Assert.Equal(4L, pageRange.StartByte);
			Assert.Equal(10L, pageRange.EndByte);

			storage.Batch(accessor => pageRange = accessor.GetPageRangeBetweenBytes("file", 4, 10));

			Assert.Equal(2, pageRange.OrderedPages.Count);
			Assert.Equal(2, pageRange.OrderedPages.First().Id);
			Assert.Equal(3, pageRange.OrderedPages.Last().Id);
			Assert.Equal(4L, pageRange.StartByte);
			Assert.Equal(10L, pageRange.EndByte);

			storage.Batch(accessor => pageRange = accessor.GetPageRangeBetweenBytes("file", 9, 14));

			Assert.Equal(1, pageRange.OrderedPages.Count);
			Assert.Equal(4, pageRange.OrderedPages.First().Id);
			Assert.Equal(4, pageRange.OrderedPages.Last().Id);
			Assert.Equal(11L, pageRange.StartByte);
			Assert.Equal(14L, pageRange.EndByte);

			storage.Batch(accessor => pageRange = accessor.GetPageRangeBetweenBytes("file", 0, 14));

			Assert.Equal(4, pageRange.OrderedPages.Count);
			Assert.Equal(1, pageRange.OrderedPages.First().Id);
			Assert.Equal(4, pageRange.OrderedPages.Last().Id);
			Assert.Equal(0L, pageRange.StartByte);
			Assert.Equal(14L, pageRange.EndByte);
		}

		public void Dispose()
		{
			storage.Dispose();
		}
	}
}
