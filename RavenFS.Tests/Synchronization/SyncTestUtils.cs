namespace RavenFS.Tests.Synchronization
{
	using RavenFS.Client;
	using System;
	using System.IO;
	using Xunit;

	public class SyncTestUtils
    {
		public static SynchronizationReport ResolveConflictAndSynchronize(RavenFileSystemClient sourceClient, RavenFileSystemClient destinationClient, string fileName)
        {
			var shouldBeConflict = sourceClient.Synchronization.StartSynchronizationToAsync(fileName, destinationClient.ServerUrl).Result;

			Assert.NotNull(shouldBeConflict.Exception);

			destinationClient.Synchronization.ResolveConflictAsync(fileName, ConflictResolutionStrategy.RemoteVersion).Wait();
			return sourceClient.Synchronization.StartSynchronizationToAsync(fileName, destinationClient.ServerUrl).Result;
        }

		public static Exception ExecuteAndGetInnerException(Action action)
		{
			Exception innerException = null;

			try
			{
				action();
			}
			catch (AggregateException exception)
			{
				innerException = exception.InnerException;
			}

			return innerException;
		}

		public static MemoryStream PrepareSourceStream(int lines)
		{
			var ms = new MemoryStream();
			var writer = new StreamWriter(ms);

			for (var i = 1; i <= lines; i++)
			{
				for (var j = 0; j < 100; j++)
				{
					writer.Write(i.ToString("D4"));
				}
				writer.Write("\n");
			}
			writer.Flush();

			return ms;
		}
    }
}
