using RavenFS.Client;

namespace RavenFS.Tests.RDC
{
	using System;
	using System.IO;
	using System.Threading;
	using RavenFS.Notifications;
	using Rdc.Conflictuality;
	using Util;
	using Xunit;

	public class RdcTestUtils
    {
		static RdcTestUtils()
		{
			Destinations = new DestinationsSynchronizationUtils();
		}

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

		public static DestinationsSynchronizationUtils Destinations { get; set; }

		public class DestinationsSynchronizationUtils
		{
			public SynchronizationReport WaitForSynchronizationFinishOnDestination(RavenFileSystemClient destinationClient, string fileName)
			{
				SynchronizationReport report;
				do
				{
					report = destinationClient.Synchronization.GetSynchronizationStatusAsync(fileName).Result;
					Thread.Sleep(50);
				} while (report == null);

				return report;
			}

			public ConflictItem WaitForConflict(RavenFileSystemClient destinationClient, string fileName)
			{
				ConflictItem conflict = null;
				do
				{
					var result = destinationClient.Config.GetConfig(SynchronizationHelper.ConflictConfigNameForFile(fileName)).Result;
					if (result != null)
					{
						conflict = new TypeHidingJsonSerializer().Parse<ConflictItem>(result["value"]);
					}
					Thread.Sleep(50);
				} while (conflict == null);

				return conflict;
			}
		}
    }
}
