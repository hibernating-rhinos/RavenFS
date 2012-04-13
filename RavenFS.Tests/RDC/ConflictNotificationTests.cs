namespace RavenFS.Tests.RDC
{
	using System;
	using System.Collections.Specialized;
	using System.Reactive.Linq;
	using System.Reactive.Threading.Tasks;
	using Client;
	using Rdc.Utils.IO;
	using Xunit;

	public class ConflictNotificationTests : MultiHostTestBase
	{
        [Fact(Skip = "Doesn't work")]
		public void NotificationsIsReceivedWhenConflictIsDetected()
		{
			RavenFileSystemClient seedClient = NewClient(0);
            RavenFileSystemClient sourceClient = NewClient(1);

            var sourceContent = new RandomlyModifiedStream(new RandomStream(1, 1), 0.01);
            var seedContent = new RandomlyModifiedStream(new RandomStream(1, 1), 0.01);

			var sourceMetadata = new NameValueCollection
                               {
                                   {"SomeTest-metadata", "some-value"}
                               };

            var seedMetadata = new NameValueCollection
                               {
                                   {"SomeTest-metadata", "should-be-overwritten"}
                               };

            seedClient.UploadAsync("abc.txt", seedMetadata, seedContent).Wait();
            sourceClient.UploadAsync("abc.txt", sourceMetadata, sourceContent).Wait();

			seedClient.Notifications.Connect().Wait();

			var notificationTask = seedClient.Notifications.ConflictDetections().Timeout(TimeSpan.FromSeconds(5)).Take(1).ToTask();

			try
			{
				seedClient.StartSynchronizationAsync(sourceClient.ServerUrl, "abc.txt").Wait();
			}
			catch
			{
				// pass
			}
			
			var conflictDetected = notificationTask.Result;

			Assert.Equal("abc.txt", conflictDetected.FileName);
			Assert.Equal(seedClient.ServerUrl, conflictDetected.ServerUrl);
		}
	}
}