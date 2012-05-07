using RavenFS.Client;

namespace RavenFS.Tests.RDC
{
	using System;
	using Xunit;

	public class RdcTestUtils
    {
		public static SynchronizationReport ResolveConflictAndSynchronize(RavenFileSystemClient sourceClient, RavenFileSystemClient destinationClient, string fileName)
        {
			var shouldBeConflict = sourceClient.Synchronization.StartSynchronizationToAsync(fileName, destinationClient.ServerUrl).Result;

			Assert.NotNull(shouldBeConflict.Exception);

			destinationClient.Synchronization.ResolveConflictAsync(sourceClient.ServerUrl, fileName, ConflictResolutionStrategy.RemoteVersion).Wait();
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
    }
}
