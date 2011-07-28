using System;
using System.Collections.Specialized;
using System.IO;
using Raven.Database.Extensions;
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
			storage.Batch(accessor =>
			{
				accessor.InsertPage(new byte[] {1, 2, 3, 4, 5, 6}, 1, 4);
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
