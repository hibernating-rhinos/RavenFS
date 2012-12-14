namespace RavenFS.Tests.Synchronization
{
	using System;
	using System.Collections.Specialized;
	using System.Net;
	using System.Reactive.Linq;
	using System.Reactive.Threading.Tasks;
	using Client;
	using Client.Changes;
	using IO;
	using Xunit;

	public class ConflictNotificationTests : MultiHostTestBase
	{
		private readonly RavenFileSystemClient destinationClient;
		private readonly RavenFileSystemClient sourceClient;

		public ConflictNotificationTests()
		{
			destinationClient = NewClient(0);
			sourceClient = NewClient(1);
		}

		[Fact]
		public void NotificationIsReceivedWhenConflictIsDetected()
		{
            var sourceContent = new RandomlyModifiedStream(new RandomStream(1), 0.01);
            var destinationContent = new RandomlyModifiedStream(sourceContent, 0.01);

            var sourceMetadata = new NameValueCollection
                {
                    {"SomeTest-metadata", "some-value"}
                };

            var destinationMetadata = new NameValueCollection
                {
                    {"SomeTest-metadata", "should-be-overwritten"}
                };

            destinationClient.UploadAsync("abc.txt", destinationMetadata, destinationContent).Wait();
            sourceClient.UploadAsync("abc.txt", sourceMetadata, sourceContent).Wait();

            var notificationTask =
                destinationClient.Notifications.Conflicts().OfType<ConflictDetected>().Timeout(TimeSpan.FromSeconds(5)).Take(1).ToTask();
            destinationClient.Notifications.WhenSubscriptionsActive().Wait();

            sourceClient.Synchronization.StartAsync("abc.txt", destinationClient.ServerUrl).Wait();

            var conflictDetected = notificationTask.Result;

            Assert.Equal("abc.txt", conflictDetected.FileName);
            Assert.Equal(new Uri(sourceClient.ServerUrl).Port, new Uri(conflictDetected.SourceServerUrl).Port);
		}

		public override void Dispose()
		{
			(destinationClient.Notifications as ServerNotifications).DisposeAsync().Wait();
			(sourceClient.Notifications as ServerNotifications).DisposeAsync().Wait();
			base.Dispose();
		}
	}
}