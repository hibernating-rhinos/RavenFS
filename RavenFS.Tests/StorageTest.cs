using System;
using System.Collections.Specialized;
using RavenFS.Extensions;
using RavenFS.Storage;

namespace RavenFS.Tests
{
	public class StorageTest : IDisposable
	{
		protected readonly TransactionalStorage transactionalStorage;

		public StorageTest()
		{
			IOExtensions.DeleteDirectory("test");
			transactionalStorage = new TransactionalStorage("test", new NameValueCollection());
			transactionalStorage.Initialize();
		}

		public void Dispose()
		{
			transactionalStorage.Dispose();
		}
	}
}