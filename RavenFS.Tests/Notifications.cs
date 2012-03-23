using System;
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
        [Fact]
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

        [Fact]
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

        [Fact]
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

        [Fact]
        public void NotificationReceivedWhenFileRenamed()
        {
            var client = NewClient();
            client.UploadAsync("abc.txt", new MemoryStream()).Wait();
            client.Notifications.Connect().Wait();

            var notificationTask = client.Notifications.FolderChanges("/").Timeout(TimeSpan.FromSeconds(2)).Take(1).ToTask();

            client.RenameAsync("abc.txt", "newName.txt").Wait();

            var fileChange = notificationTask.Result;

            Assert.Equal("newName.txt", fileChange.File);
            Assert.Equal(FileChangeAction.Rename, fileChange.Action);
        }

        [Fact]
        public void NotificationsAreOnlyReceivedForFilesInGivenFolder()
        {
            var client = NewClient();
            client.Notifications.Connect().Wait();

            var notificationTask = client.Notifications.FolderChanges("/Folder").Buffer(TimeSpan.FromSeconds(2)).Take(1).ToTask();

            client.UploadAsync("AnotherFolder/abc.txt", new MemoryStream()).Wait();

            var notifications = notificationTask.Result;

            Assert.Equal(0, notifications.Count);
        }
    }
}
