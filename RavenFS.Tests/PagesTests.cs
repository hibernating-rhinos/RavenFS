using System;
using System.Collections.Specialized;
using System.IO;
using Raven.Database.Extensions;
using RavenFS.Util;
using Xunit;

namespace RavenFS.Tests
{
	public class PagesTests : IDisposable
	{
		readonly Storage.Storage storage;

		public PagesTests()
		{
			IOExtensions.DeleteDirectory("test");
			storage = new Storage.Storage("test", new NameValueCollection());
			storage.Initialize();
		}

		[Fact]
		public void CanInsertPage()
		{
			storage.Batch(accessor => accessor.InsertPage(new byte[] { 1, 2, 3, 4, 5, 6 }, 4));
		}

		[Fact]
		public void CanInsertAndReadPage()
		{
			HashKey key = null;
			storage.Batch(accessor =>
			{
				key = accessor.InsertPage(new byte[] { 1, 2, 3, 4, 5, 6 }, 4);
			});

			storage.Batch(accessor =>
			{
				var buffer = new byte[4];
				Assert.Equal(4, accessor.ReadPage(key, buffer, 0));
				Assert.Equal(new byte[] { 1, 2, 3, 4 }, buffer);
			});
		}

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		/// <filterpriority>2</filterpriority>
		public void Dispose()
		{
			storage.Dispose();
		}
	}
}
