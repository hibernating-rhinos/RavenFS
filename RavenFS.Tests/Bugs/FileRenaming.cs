using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using Xunit;

namespace RavenFS.Tests.Bugs
{
    public class FileRenaming : StorageTest
    {
        [Fact]
        public void Should_rename_file_and_content()
        {
            transactionalStorage.Batch(
                accessor =>
                    {
                        accessor.PutFile("test.bin", 3, new NameValueCollection());
                        var pageId = accessor.InsertPage(new byte[] {1, 2, 3}, 3);
                        accessor.AssociatePage("test.bin", pageId, 0, 3);
                        accessor.CompleteFileUpload("test.bin");
                    });

            transactionalStorage.Batch(
                accessor => accessor.RenameFile("test.bin", "test.result.bin"));

            transactionalStorage.Batch(
                accessor =>
                    {
                        var pages = accessor.GetFile("test.result.bin", 0, 1);
                        var buffer = new byte[3];
                        accessor.ReadPage(pages.Pages.First().Id, buffer);
                        Assert.Equal(1, buffer[0]);
                    });

        }
    }
}
