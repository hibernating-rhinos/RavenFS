#Synchronization client

All actions related to a synchronization process that are available from the client you will find under `RavenFileSystemClient.Synchronization` property which is type of `SynchronizationClient`.

##Performing synchronization

If you setup a server to synchronize file updates to configured destination servers, then all updates will be propagated automatically. The synchronization acts in the background on the servers and you don't have to worry about it.
However the Client API offers two methods that allow you to start synchronization manually.

If you want to synchronize all servers saved in `Raven/Synchronization/Destinations` irrespective of the internal synchronization cycle perform an action:
{CODE-START:csharp/}
using (var client = new RavenFileSystemClient("http://ravenfs-1:9090"))
{
	DestinationSyncResult[] reports =
		await client.Synchronization.SynchronizeDestinationsAsync(forceSyncingAll: false);
}
{CODE-END /}
Performing this action will cause that files changed since last synchronization cycle will be propagated. Note the optional *forceSyncingAll* parameter. When is set to *true* each finished synchronization work checks if there is any pending file synchronization, if yes and the server has available synchronization slots (controlled by [`Raven-Synchronization-Limit`](../synchronization/configurations#raven-synchronization-limit) config) starts to do it. 
If this parameter value is *false* the mentioned condition is not even checked.

The second opportunity to force synchronization to an another server is to call:
{CODE-START:csharp/}
using (var client = new RavenFileSystemClient("http://ravenfs-1:9090"))
{
	SynchronizationReport report = 
		await client.Synchronization.StartAsync("file.bin", "http://ravenfs-2:9091");
}
{CODE-END /}

As you can see by using this method you can control exactly what file you want to synchronize and to what server (even though it was not configured as a destination).
The returned result is `SynchronizationReport` that contains the following fields:

* a name of a file,
* an ETag file value,
* an exception if something went wrong during the operation,
* a type of the synchronization (ContentUpdate, MetadataUpdate, Rename, Delete),
* numbers of transferred and copied bytes and a length of need list (filled only if [RDC stuff](../synchronization/synchronization-types/content-update) was performed).

##Monitoring

There are several methods to check the status of the synchronization process generally or for a particular file. 

To get the status of pushing file modifications on a source server use methods:
{CODE-START:csharp/}
ListPage<SynchronizationDetails> active = await client.Synchronization.GetActiveAsync();

ListPage<SynchronizationDetails> page = await client.Synchronization.GetPendingAsync();
{CODE-END /}

Whereas to fetch information about already finished synchronization operation on a destination server call:
{CODE-START:csharp/}
ListPage<SynchronizationReport> finished = await client.Synchronization.GetFinishedAsync();
{CODE-END /}
All of the methods above return paged results, you can control paging by using optional methods parameters.

You are also able to fetch the `SynchronizationReport` for a single file:
{CODE-START:csharp/}
SynchronizationReport synchronizationReport = 
	await client.Synchronization.GetSynchronizationStatusAsync("file.bin");
{CODE-END /}

##Conflicts

Conflicts appear when an attempt to synchronize files with different version and history is taken. Below is a sample code that shows how we can create a conflict. First we upload two files with the same name to both servers and next try to synchronize from the first one to the second one. 
Files have independent versions so conflict is created on the destination server. We call `StartAsync` method manually but the same would happen if a synchronization between the servers would be configured and the source would try to synchronize after file upload.
{CODE-START:csharp/}
using (var sourceClient = new RavenFileSystemClient("http://ravenfs-1:9091"))
using (var destinationClient = new RavenFileSystemClient("http://ravenfs-2:9092"))
{
	await sourceClient.UploadAsync("sample.bin", new MemoryStream(new byte[] {1}));
	await destinationClient.UploadAsync("sample.bin", new MemoryStream(new byte[] {1}));

	SynchronizationReport report = 
		await sourceClient.Synchronization.StartAsync("sample.bin", destinationClient.ServerUrl);

	Console.WriteLine("Report exception: {0}", report.Exception.Message);

	NameValueCollection metadata = await destinationClient.GetMetadataForAsync("sample.bin");

	Console.WriteLine("File conflict set to: {0}", metadata["Raven-Synchronization-Conflict"]);

	var conflicts = await destinationClient.Synchronization.GetConflictsAsync();

	Console.WriteLine("File {0} conflicted with {1}", conflicts.Items[0].FileName, conflicts.Items[0].RemoteServerUrl);
}
{CODE-END /}

The output is following:

	Report exception: File sample.bin is conflicted
	File conflict set to: True
	File sample.bin conflicted with http://ravenfs-1:9091/

Now we can resolve on the destination server the conflict by using one of the two strategies:

* RemoteVersion - take the file from the source server,
* CurrentVersion - keep my version of the file.

Let's resolve by using `RemoteVersion`. We will talk the destination server that it can accept the remote server file version, so next attempt to synchronize from the source to the destination should pass.

{CODE-START:csharp/}
using (var sourceClient = new RavenFileSystemClient("http://ravenfs-1:9091"))
using (var destinationClient = new RavenFileSystemClient("http://ravenfs-2:9092"))
{
	await destinationClient.Synchronization.ResolveConflictAsync("sample.bin", ConflictResolutionStrategy.RemoteVersion);

	SynchronizationReport report =
		await sourceClient.Synchronization.StartAsync("sample.bin", destinationClient.ServerUrl);

	Console.WriteLine("Report exception: {0}", report.Exception != null ? report.Exception.Message : "null");

	NameValueCollection metadata = await destinationClient.GetMetadataForAsync("sample.bin");

	Console.WriteLine("File conflict set to: {0}", metadata["Raven-Synchronization-Conflict"] ?? "null");

	var conflicts = await destinationClient.Synchronization.GetConflictsAsync();

	Console.WriteLine("Number of conflicts: {0}", conflicts.TotalCount);
}
{CODE-END /}

The output of this conflict resolving code is:

	Report exception: null
	File conflict set to: null
	Number of conflicts: 0

what means that source server succeeded in synchronization to the destination.

If you want to keep the destination server file resolve by `CurrentVersion`. Then if your configuration is master-master next synchronization cycle will transfer the file according to the direction of conflict resolution that is to "the source" in our case. 
You can also force the synchronization from the destination like in example below:

{CODE-START:csharp/}
using (var sourceClient = new RavenFileSystemClient("http://ravenfs-1:9091"))
using (var destinationClient = new RavenFileSystemClient("http://ravenfs-2:9092"))
{
	await destinationClient.Synchronization.ResolveConflictAsync("sample.bin", ConflictResolutionStrategy.CurrentVersion);

	SynchronizationReport report =
		await destinationClient.Synchronization.StartAsync("sample.bin", sourceClient.ServerUrl);

	Console.WriteLine("Report exception: {0}", report.Exception != null ? report.Exception.Message : "null");

	NameValueCollection metadata = await sourceClient.GetMetadataForAsync("sample.bin");

	Console.WriteLine("File conflict set to: {0}", metadata["Raven-Synchronization-Conflict"] ?? "null");

	var conflicts = await sourceClient.Synchronization.GetConflictsAsync();

	Console.WriteLine("Number of conflicts: {0}", conflicts.TotalCount);
}
{CODE-END /}

The output (note that here we asked "the source" for conflict):
	
	Report exception: null
	File conflict set to: null
	Number of conflicts: 0
