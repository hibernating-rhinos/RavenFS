#Rename and download

##Rename

The rename operation requires to pass an existing file name and a new name:

{CODE-START:csharp/}
client.RenameAsync("one.bin", "two.bin");
{CODE-END /}

##Delete

In order to delete an existing file type the following code:

{CODE-START:csharp/}
client.DeleteAsync("pictures/wallpaper.bin");
{CODE-END /}

##Forcing storage tasks

The section [Background tasks](../../server/background-tasks) describes the process of resuming delete and rename operations. The Client API also provides a methods to force the tasks execution manually.

If you want to force a delete operations use:
{CODE-START:csharp/}
client.Storage.CleanUp();
{CODE-END /}
To manually force rename operations execute the code below:
{CODE-START:csharp/}
client.Storage.RetryRenaming();
{CODE-END /}
