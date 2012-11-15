#Notifications

RavenFS offers a notification mechanism to allow track the client what is currently happening on the server. You are able to track the following file system actions:

* file changes in a particular folder,
* configuration changes,
* synchronization start/end,
* conflict detections.

Every available notification in the Client API that you can subscribe returns `IObservable<T>` where `T` is notification type. To work conveniently and efficiently with notifications add and use [Reactive Extensions](http://nuget.org/packages/Rx-Main) library in you project.

##Folder changes

You are able to subscribe file changes in a selected directory:

{CODE-START:csharp/}
client.Notifications.FolderChanges("/").Subscribe(fileChange =>
{
	Console.WriteLine("File name: {0}. Change type: {1}", fileChange.File, fileChange.Action);
});
{CODE-END /}

From now every time any change will occur in main `/` folder you will get `FileChange` notification which contains two properties:

* a file name that this notification involves to,
* a performed action type - one of the following: `Add, Delete, Update, Renaming, Renamed`.


##Configuration changes

The same way like observing file modifications you can track configuration updates:

{CODE-START:csharp/}
client.Notifications.ConfigurationChanges().Subscribe(configChange =>
{
	Console.WriteLine("Config name: {0}. Change type: {1}", configChange.Name, configChange.Action);
});
{CODE-END /}

`ConfigChange` notification has two information:

* a configuration name,
* an action type - possible values are: `Set` and `Delete`.

##Synchronization monitoring

You can also subscribe notifications which relate to synchronization operations. A source server as well as a destination server publish these types of notifications.

* The source server creates them when:
    * starts to perform a synchronization (that is right before a synchronization request to the destination is sent),
	* a synchronization is finished (the destination responded to the request).
* The destination publish notification when:
	* starts to process synchronization request,
	* a synchronization is done.

The notification mechanism does not take into account whether there were any errors during synchronization or not, the notification is always send.

{CODE-START:csharp/}
client.Notifications.SynchronizationUpdates().Subscribe(syncNotification =>
{
	Console.WriteLine("File: {0}, type: {1}, action: {2}, direction: {3}", syncNotification.FileName,
					    syncNotification.Type, syncNotification.Action, syncNotification.SynchronizationDirection);
});
{CODE-END /}

The `SynchronizationUpdate` object notification contains a full information about perfomed operation:

* a name of a file which a synchronization is performed for,
* a synchronization type (see an [appropriate section](../synchronization/synchronization-types)),
* a type of action that is currently taken - `Start` or `End`,
* a direction of a syncing which says which server sent the notification - `Outgoing` (source) or `Incoming` (destination),
* a source server url,
* a source server identifier,
* a destination server url.

##Conflict detections

RavenFS is also ready to send notification if any file conflict in a synchronization process is detected. Note that conflicts are always detected on the destination server, so only from there notifications can be retrieved.
Here is sample usage of the conflict detection tracking:

{CODE-START:csharp/}
client.Notifications.ConflictDetected().Subscribe(conflict =>
{
	Console.WriteLine("File: {0}, source server: {1}", conflict.FileName, conflict.SourceServerUrl);
});
{CODE-END /}

`ConflictDetected` object has:
* a name of a file,
* an address of source server.