namespace RavenFS.Tests.Synchronization
{
	using System;
	using System.Collections.Specialized;
	using System.IO;
	using System.Reactive.Linq;
	using System.Reactive.Threading.Tasks;
	using RavenFS.Client;
	using Xunit;

	public class SynchronizationNotificationTests : MultiHostTestBase
	{
		[Fact(Skip = "When running the build script from command line notification tests cause the crash")]
		public void NotificationsAreReceivedOnSourceWhenSynchronizationsAreStartedAndFinished()
		{
			var source = NewClient(0);
			var destination = NewClient(1);

			source.Notifications.Connect().Wait();

			// content update
			source.UploadAsync("test.bin", new MemoryStream(new byte[] {1, 2, 3})).Wait();

			var notificationTask =
				source.Notifications.SynchronizationUpdates(SynchronizationDirection.Outgoing).Timeout(TimeSpan.FromSeconds(20)).Take(2).ToArray().
					ToTask();

			var report = source.Synchronization.StartSynchronizationToAsync("test.bin", destination.ServerUrl).Result;

			Assert.Null(report.Exception);

			var synchronizationUpdates = notificationTask.Result;

			Assert.Equal(SynchronizationAction.Start, synchronizationUpdates[0].Action);
			Assert.Equal("test.bin", synchronizationUpdates[0].FileName);
			Assert.Equal(SynchronizationType.ContentUpdate, synchronizationUpdates[0].Type);
			Assert.Equal(SynchronizationAction.Finish, synchronizationUpdates[1].Action);
			Assert.Equal("test.bin", synchronizationUpdates[1].FileName);
			Assert.Equal(SynchronizationType.ContentUpdate, synchronizationUpdates[1].Type);

			// metadata update
			source.UpdateMetadataAsync("test.bin", new NameValueCollection() {{"key", "value"}}).Wait();

			notificationTask =
				source.Notifications.SynchronizationUpdates(SynchronizationDirection.Outgoing).Timeout(TimeSpan.FromSeconds(20)).
					Take(2).ToArray().
					ToTask();

			report = source.Synchronization.StartSynchronizationToAsync("test.bin", destination.ServerUrl).Result;

			Assert.Null(report.Exception);

			synchronizationUpdates = notificationTask.Result;

			Assert.Equal(SynchronizationAction.Start, synchronizationUpdates[0].Action);
			Assert.Equal("test.bin", synchronizationUpdates[0].FileName);
			Assert.Equal(SynchronizationType.MetadataUpdate, synchronizationUpdates[0].Type);
			Assert.Equal(SynchronizationAction.Finish, synchronizationUpdates[1].Action);
			Assert.Equal("test.bin", synchronizationUpdates[1].FileName);
			Assert.Equal(SynchronizationType.MetadataUpdate, synchronizationUpdates[1].Type);

			// rename update
			source.RenameAsync("test.bin", "rename.bin").Wait();

			notificationTask =
				source.Notifications.SynchronizationUpdates(SynchronizationDirection.Outgoing).Timeout(TimeSpan.FromSeconds(20)).
					Take(2).ToArray().
					ToTask();

			report = source.Synchronization.StartSynchronizationToAsync("test.bin", destination.ServerUrl).Result;

			Assert.Null(report.Exception);

			synchronizationUpdates = notificationTask.Result;

			Assert.Equal(SynchronizationAction.Start, synchronizationUpdates[0].Action);
			Assert.Equal("test.bin", synchronizationUpdates[0].FileName);
			Assert.Equal(SynchronizationType.Rename, synchronizationUpdates[0].Type);
			Assert.Equal(SynchronizationAction.Finish, synchronizationUpdates[1].Action);
			Assert.Equal("test.bin", synchronizationUpdates[1].FileName);
			Assert.Equal(SynchronizationType.Rename, synchronizationUpdates[1].Type);

			// delete update
			source.DeleteAsync("rename.bin").Wait();

			notificationTask =
				source.Notifications.SynchronizationUpdates(SynchronizationDirection.Outgoing).Timeout(TimeSpan.FromSeconds(20)).
					Take(2).ToArray().
					ToTask();

			report = source.Synchronization.StartSynchronizationToAsync("rename.bin", destination.ServerUrl).Result;

			Assert.Null(report.Exception);

			synchronizationUpdates = notificationTask.Result;

			Assert.Equal(SynchronizationAction.Start, synchronizationUpdates[0].Action);
			Assert.Equal("rename.bin", synchronizationUpdates[0].FileName);
			Assert.Equal(SynchronizationType.Delete, synchronizationUpdates[0].Type);
			Assert.Equal(SynchronizationAction.Finish, synchronizationUpdates[1].Action);
			Assert.Equal("rename.bin", synchronizationUpdates[1].FileName);
			Assert.Equal(SynchronizationType.Delete, synchronizationUpdates[1].Type);
		}

		[Fact(Skip = "When running the build script from command line notification tests cause the crash")]
		public void NotificationsAreReceivedOnDestinationWhenSynchronizationsAreFinished()
		{
			var source = NewClient(0);
			var destination = NewClient(1);

			destination.Notifications.Connect().Wait();

			// content update
			source.UploadAsync("test.bin", new MemoryStream(new byte[] { 1, 2, 3 })).Wait();

			var notificationTask =
				destination.Notifications.SynchronizationUpdates(SynchronizationDirection.Incoming).Timeout(TimeSpan.FromSeconds(20)).Take(1).ToArray().
					ToTask();

			var report = source.Synchronization.StartSynchronizationToAsync("test.bin", destination.ServerUrl).Result;

			Assert.Null(report.Exception);

			var synchronizationUpdates = notificationTask.Result;

			Assert.Equal(SynchronizationAction.Finish, synchronizationUpdates[0].Action);
			Assert.Equal("test.bin", synchronizationUpdates[0].FileName);
			Assert.Equal(SynchronizationType.ContentUpdate, synchronizationUpdates[0].Type);

			// metadata update
			source.UpdateMetadataAsync("test.bin", new NameValueCollection() { { "key", "value" } }).Wait();

			notificationTask =
				destination.Notifications.SynchronizationUpdates(SynchronizationDirection.Incoming).Timeout(TimeSpan.FromSeconds(20)).Take(1).ToArray().
					ToTask();

			report = source.Synchronization.StartSynchronizationToAsync("test.bin", destination.ServerUrl).Result;

			Assert.Null(report.Exception);

			synchronizationUpdates = notificationTask.Result;

			Assert.Equal(SynchronizationAction.Finish, synchronizationUpdates[0].Action);
			Assert.Equal("test.bin", synchronizationUpdates[0].FileName);
			Assert.Equal(SynchronizationType.MetadataUpdate, synchronizationUpdates[0].Type);

			// rename update
			source.RenameAsync("test.bin", "rename.bin").Wait();

			notificationTask =
				destination.Notifications.SynchronizationUpdates(SynchronizationDirection.Incoming).Timeout(TimeSpan.FromSeconds(20)).Take(1).ToArray().
					ToTask();

			report = source.Synchronization.StartSynchronizationToAsync("test.bin", destination.ServerUrl).Result;

			Assert.Null(report.Exception);

			synchronizationUpdates = notificationTask.Result;

			Assert.Equal(SynchronizationAction.Finish, synchronizationUpdates[0].Action);
			Assert.Equal("test.bin", synchronizationUpdates[0].FileName);
			Assert.Equal(SynchronizationType.Rename, synchronizationUpdates[0].Type);

			// delete update
			source.DeleteAsync("rename.bin").Wait();

			notificationTask =
				destination.Notifications.SynchronizationUpdates(SynchronizationDirection.Incoming).Timeout(TimeSpan.FromSeconds(20)).Take(1).ToArray().
					ToTask();

			report = source.Synchronization.StartSynchronizationToAsync("rename.bin", destination.ServerUrl).Result;

			Assert.Null(report.Exception);

			synchronizationUpdates = notificationTask.Result;

			Assert.Equal(SynchronizationAction.Finish, synchronizationUpdates[0].Action);
			Assert.Equal("rename.bin", synchronizationUpdates[0].FileName);
			Assert.Equal(SynchronizationType.Delete, synchronizationUpdates[0].Type);
		}
	}
}