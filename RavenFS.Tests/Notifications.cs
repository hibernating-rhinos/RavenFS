using System;
using System.Collections.Specialized;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using RavenFS.Client;
using Xunit;

namespace RavenFS.Tests
{
	using Client.Changes;

	public class Notifications : WebApiTest
    {
	    private readonly RavenFileSystemClient client;

	    public Notifications()
	    {
		    client = NewClient();
	    }

		[Fact]
        public void NotificationReceivedWhenFileAdded()
        {
            client.Notifications.ConnectionTask.Wait();

            var notificationTask =
                client.Notifications.FolderChanges("/").Timeout(TimeSpan.FromSeconds(2)).Take(1).ToTask();
            client.Notifications.WhenSubscriptionsActive().Wait();

            client.UploadAsync("abc.txt", new MemoryStream()).Wait();

            var fileChange = notificationTask.Result;

            Assert.Equal("abc.txt", fileChange.File);
            Assert.Equal(FileChangeAction.Add, fileChange.Action);
        }

		[Fact]
		public void NotificationReceivedWhenFileDeleted()
        {
            client.UploadAsync("abc.txt", new MemoryStream()).Wait();


            var notificationTask =
                client.Notifications.FolderChanges("/").Timeout(TimeSpan.FromSeconds(2)).Take(1).ToTask();
            client.Notifications.WhenSubscriptionsActive().Wait();

            client.DeleteAsync("abc.txt").Wait();

            var fileChange = notificationTask.Result;

            Assert.Equal("abc.txt", fileChange.File);
            Assert.Equal(FileChangeAction.Delete, fileChange.Action);
        }

		[Fact]
		public void NotificationReceivedWhenFileUpdated()
        {
            client.UploadAsync("abc.txt", new MemoryStream()).Wait();

            var notificationTask =
                client.Notifications.FolderChanges("/").Timeout(TimeSpan.FromSeconds(2)).Take(1).ToTask();
            client.Notifications.WhenSubscriptionsActive().Wait();

            client.UpdateMetadataAsync("abc.txt", new NameValueCollection() {{"MyMetadata", "MyValue"}}).Wait();

            var fileChange = notificationTask.Result;

            Assert.Equal("abc.txt", fileChange.File);
            Assert.Equal(FileChangeAction.Update, fileChange.Action);
        }

		[Fact]
		public void NotificationsReceivedWhenFileRenamed()
        {
            client.UploadAsync("abc.txt", new MemoryStream()).Wait();

            var notificationTask =
                client.Notifications.FolderChanges("/").Buffer(TimeSpan.FromSeconds(5)).Take(1).ToTask();
            client.Notifications.WhenSubscriptionsActive().Wait();

            client.RenameAsync("abc.txt", "newName.txt").Wait();

            var fileChanges = notificationTask.Result;

            Assert.Equal("abc.txt", fileChanges[0].File);
            Assert.Equal(FileChangeAction.Renaming, fileChanges[0].Action);
            Assert.Equal("newName.txt", fileChanges[1].File);
            Assert.Equal(FileChangeAction.Renamed, fileChanges[1].Action);
        }

		[Fact]
		public void NotificationsAreOnlyReceivedForFilesInGivenFolder()
        {
            var notificationTask =
                client.Notifications.FolderChanges("/Folder").Buffer(TimeSpan.FromSeconds(2)).Take(1).ToTask();
            client.Notifications.WhenSubscriptionsActive().Wait();

            client.UploadAsync("AnotherFolder/abc.txt", new MemoryStream()).Wait();

            var notifications = notificationTask.Result;

            Assert.Equal(0, notifications.Count);
        }

		[Fact]
		public void NotificationsIsReceivedWhenConfigIsUpdated()
        {
            var notificationTask =
                client.Notifications.ConfigurationChanges().Timeout(TimeSpan.FromSeconds(2)).Take(1).ToTask();
            client.Notifications.WhenSubscriptionsActive().Wait();

            client.Config.SetConfig("Test", new NameValueCollection()).Wait();

            var configChange = notificationTask.Result;

            Assert.Equal("Test", configChange.Name);
            Assert.Equal(ConfigChangeAction.Set, configChange.Action);
        }

		[Fact]
		public void NotificationsIsReceivedWhenConfigIsDeleted()
        {
            var notificationTask =
                client.Notifications.ConfigurationChanges().Timeout(TimeSpan.FromSeconds(2)).Take(1).ToTask();
            client.Notifications.WhenSubscriptionsActive().Wait();

            client.Config.DeleteConfig("Test").Wait();

            var configChange = notificationTask.Result;

            Assert.Equal("Test", configChange.Name);
            Assert.Equal(ConfigChangeAction.Delete, configChange.Action);
        }

		public override void Dispose()
		{
			(client.Notifications as ServerNotifications).DisposeAsync().Wait();
			base.Dispose();
		}
    }
}
