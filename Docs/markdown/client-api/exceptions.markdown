#Exceptions

##FileNotFoundException

This exception is thrown if you attempt to perform a file operation and the file that is not existing on a server.

{CODE-START:csharp/}
using (var client = new RavenFileSystemClient("http://localhost:9090"))
{
	try
	{
		await client.DeleteAsync("file.bin");
	}
	catch (FileNotFoundException)
	{
		log.Error("File 'file.bin' is not existing");
	}
}
{CODE-END /}

##SynchronizationException

RavenFS will not allow you to modify a file if it is being synchronized. If the file is locked any attempts to change it will fail and result as `SynchronizationException`.

##ConcurrencyException

You have to be aware that RavenFS uses transactional storage to keep data. This exception is thrown if there are multiple modifications to the same data at the same time
 e.g. multiple clients try to change the same file or files that are modified share the same data ([Pages concept](../intro/files-in-ravenfs#Pages)).

##InvalidOperationException

If it happens any general RavenFS error you'll get `InvalidOperationException`. More details about the error are provided with `Message` exception property.

{CODE-START:csharp/}
using (var client = new RavenFileSystemClient("http://localhost:9090"))
{
	try
	{
		await client.RenameAsync("file.bin", "rename.bin");
	}
	catch (InvalidOperationException ex)
	{
		log.ErrorException(ex.Message);
	}
}
{CODE-END /}