using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using RavenFS.Client;
using Xunit;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;

namespace RavenFS.Tests
{
    public class Notifications : WebApiTest
    {
		[Fact(/*Skip = "When running the build script from command line notification tests cause the crash"*/)]
        public void NotificationReceivedWhenFileAdded()
        {
            using(var client = NewClient())
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
        }

		[Fact(/*Skip = "When running the build script from command line notification tests cause the crash"*/)]
		public void NotificationReceivedWhenFileDeleted()
        {
            using (var client = NewClient())
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
        }

		[Fact(/*Skip = "When running the build script from command line notification tests cause the crash"*/)]
		public void NotificationReceivedWhenFileUpdated()
        {
            using (var client = NewClient())
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
        }

		[Fact(/*Skip = "When running the build script from command line notification tests cause the crash"*/)]
		public void NotificationsReceivedWhenFileRenamed()
        {
            using (var client = NewClient())
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
        }

		[Fact(/*Skip = "When running the build script from command line notification tests cause the crash"*/)]
		public void NotificationsAreOnlyReceivedForFilesInGivenFolder()
        {
            using (var client = NewClient())
            {
                var notificationTask =
                    client.Notifications.FolderChanges("/Folder").Buffer(TimeSpan.FromSeconds(2)).Take(1).ToTask();
                client.Notifications.WhenSubscriptionsActive().Wait();

                client.UploadAsync("AnotherFolder/abc.txt", new MemoryStream()).Wait();

                var notifications = notificationTask.Result;

                Assert.Equal(0, notifications.Count);
            }
        }

		[Fact(/*Skip = "When running the build script from command line notification tests cause the crash"*/)]
		public void NotificationsIsReceivedWhenConfigIsUpdated()
        {
            using (var client = NewClient())
            {
                var notificationTask =
                    client.Notifications.ConfigurationChanges().Timeout(TimeSpan.FromSeconds(2)).Take(1).ToTask();
                client.Notifications.WhenSubscriptionsActive().Wait();

                client.Config.SetConfig("Test", new NameValueCollection()).Wait();

                var configChange = notificationTask.Result;

                Assert.Equal("Test", configChange.Name);
                Assert.Equal(ConfigChangeAction.Set, configChange.Action);
            }
        }

		[Fact(/*Skip = "When running the build script from command line notification tests cause the crash"*/)]
		public void NotificationsIsReceivedWhenConfigIsDeleted()
        {
            using (var client = NewClient())
            {
                var notificationTask =
                    client.Notifications.ConfigurationChanges().Timeout(TimeSpan.FromSeconds(2)).Take(1).ToTask();
                client.Notifications.WhenSubscriptionsActive().Wait();

                client.Config.DeleteConfig("Test").Wait();

                var configChange = notificationTask.Result;

                Assert.Equal("Test", configChange.Name);
                Assert.Equal(ConfigChangeAction.Delete, configChange.Action);
            }
        }
    }
}
