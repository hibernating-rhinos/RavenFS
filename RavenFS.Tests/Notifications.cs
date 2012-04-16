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
        [Fact(Skip = "Doesn't work")]
        public void NotificationReceivedWhenFileAdded()
        {
            var client = NewClient();
            client.Notifications.Connect().Wait();

            var notificationTask = client.Notifications.FolderChanges("/").Timeout(TimeSpan.FromSeconds(2)).Take(1).ToTask();

            client.UploadAsync("abc.txt", new MemoryStream()).Wait();

            var fileChange = notificationTask.Result;

            Assert.Equal("abc.txt", fileChange.File);
            Assert.Equal(FileChangeAction.Add, fileChange.Action);
        }

        [Fact(Skip = "Doesn't work")]
        public void NotificationReceivedWhenFileDeleted()
        {
            var client = NewClient();
            client.UploadAsync("abc.txt", new MemoryStream()).Wait();
            client.Notifications.Connect().Wait();

            var notificationTask = client.Notifications.FolderChanges("/").Timeout(TimeSpan.FromSeconds(2)).Take(1).ToTask();

            client.DeleteAsync("abc.txt").Wait();

            var fileChange = notificationTask.Result;

            Assert.Equal("abc.txt", fileChange.File);
            Assert.Equal(FileChangeAction.Delete, fileChange.Action);
        }

        [Fact(Skip = "Doesn't work")]
        public void NotificationReceivedWhenFileUpdated()
        {
            var client = NewClient();
            client.UploadAsync("abc.txt", new MemoryStream()).Wait();
            client.Notifications.Connect().Wait();

            var notificationTask = client.Notifications.FolderChanges("/").Timeout(TimeSpan.FromSeconds(2)).Take(1).ToTask();

            client.UpdateMetadataAsync("abc.txt", new NameValueCollection() { {"MyMetadata", "MyValue"}}).Wait();

            var fileChange = notificationTask.Result;

            Assert.Equal("abc.txt", fileChange.File);
            Assert.Equal(FileChangeAction.Update, fileChange.Action);
        }

        [Fact(Skip = "Doesn't work")]
        public void NotificationsReceivedWhenFileRenamed()
        {
            var client = NewClient();
            client.UploadAsync("abc.txt", new MemoryStream()).Wait();
            client.Notifications.Connect().Wait();

            var notificationTask = client.Notifications.FolderChanges("/").Buffer(TimeSpan.FromSeconds(2)).Take(1).ToTask();

            client.RenameAsync("abc.txt", "newName.txt").Wait();

            var fileChanges = notificationTask.Result;

            Assert.Equal("abc.txt", fileChanges[0].File);
            Assert.Equal(FileChangeAction.Renaming, fileChanges[0].Action);
            Assert.Equal("newName.txt", fileChanges[1].File);
            Assert.Equal(FileChangeAction.Renamed, fileChanges[1].Action);
        }

        [Fact(Skip = "Doesn't work")]
        public void NotificationsAreOnlyReceivedForFilesInGivenFolder()
        {
            var client = NewClient();
            client.Notifications.Connect().Wait();

            var notificationTask = client.Notifications.FolderChanges("/Folder").Buffer(TimeSpan.FromSeconds(2)).Take(1).ToTask();

            client.UploadAsync("AnotherFolder/abc.txt", new MemoryStream()).Wait();

            var notifications = notificationTask.Result;

            Assert.Equal(0, notifications.Count);
        }

        [Fact(Skip = "Doesn't work")]
        public void NotificationsIsReceivedWhenConfigIsUpdated()
        {
            var client = NewClient();
            client.Notifications.Connect().Wait();

            var notificationTask = client.Notifications.ConfigChanges().Timeout(TimeSpan.FromSeconds(2)).Take(1).ToTask();

            client.Config.SetConfig("Test", new NameValueCollection()).Wait();

            var configChange = notificationTask.Result;

            Assert.Equal("Test", configChange.Name);
            Assert.Equal(ConfigChangeAction.Set, configChange.Action);
        }

        [Fact(Skip = "Doesn't work")]
        public void NotificationsIsReceivedWhenConfigIsDeleted()
        {
            var client = NewClient();
            client.Notifications.Connect().Wait();

            var notificationTask = client.Notifications.ConfigChanges().Timeout(TimeSpan.FromSeconds(2)).Take(1).ToTask();

            client.Config.DeleteConfig("Test").Wait();

            var configChange = notificationTask.Result;

            Assert.Equal("Test", configChange.Name);
            Assert.Equal(ConfigChangeAction.Delete, configChange.Action);
        }
    }
}
