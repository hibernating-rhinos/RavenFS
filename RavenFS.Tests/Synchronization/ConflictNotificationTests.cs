using System;
using System.Collections.Specialized;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using RavenFS.Client;
using RavenFS.Client.Changes;
using RavenFS.Tests.Synchronization.IO;
using Xunit;

namespace RavenFS.Tests.Synchronization
{
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
		public async void NotificationIsReceivedWhenConflictIsDetected()
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

			await destinationClient.UploadAsync("abc.txt", destinationMetadata, destinationContent);
			await sourceClient.UploadAsync("abc.txt", sourceMetadata, sourceContent);

			var notificationTask =
				destinationClient.Notifications.Conflicts()
				                 .OfType<ConflictDetected>()
				                 .Timeout(TimeSpan.FromSeconds(5))
				                 .Take(1)
				                 .ToTask();
			await destinationClient.Notifications.WhenSubscriptionsActive();

			await sourceClient.Synchronization.StartAsync("abc.txt", destinationClient.ServerUrl);

			var conflictDetected = await notificationTask;

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