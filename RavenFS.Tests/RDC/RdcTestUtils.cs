using RavenFS.Client;

namespace RavenFS.Tests.RDC
{
	using System;
	using System.IO;
	using System.Threading;
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

		public static SynchronizationReport WaitForSynchronizationFinishOnDestination(RavenFileSystemClient destinationClient, string fileName)
		{
			SynchronizationReport report;
			do
			{
				report = destinationClient.Synchronization.GetSynchronizationStatusAsync(fileName).Result;
				Thread.Sleep(50);
			} while (report == null);

			return report;
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
