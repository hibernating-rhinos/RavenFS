using System;
using System.Collections.Specialized;
using RavenFS.Extensions;
using RavenFS.Tests.Tools;
using RavenFS.Util;
using Xunit;

namespace RavenFS.Tests
{
	public class PagesTests : IDisposable
	{
		readonly Storage.TransactionalStorage storage;

		public PagesTests()
		{
			IOExtensions.DeleteDirectory("test");
			storage = new Storage.TransactionalStorage("test", new NameValueCollection());
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
				accessor.PutFile("test.csv", 12, new NameValueCollection());

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
				accessor.PutFile("test.csv", 16, new NameValueCollection());

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
				accessor.PutFile("test.csv", 16, new NameValueCollection());

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
			storage.Batch(accessor => accessor.PutFile("test.csv", 16, new NameValueCollection
			{
				{"test", "abc"}
			}));


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
				accessor.PutFile("test.csv", 16, new NameValueCollection());

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

		public void Dispose()
		{
			storage.Dispose();
		}
	}
}
