#How it works?

The synchronization is a multiple-step process. It begins if any file update has occurred and is considered as completed when a remote server confirms that a syncing operation has finished successfully. An initiator of the synchronization is the server where a file has changed - this server is called **source**. It pushes all data needed to synchronize the file to other configured servers called **destinations**. 

##Destinations

In order to determine where RavenFS instance should synchronize files to, it needs a list of destination servers. It should be stored as `Raven/Synchronization/Destinations` configuration, and have the following format:
{CODE-START:json /}
{
	"url":[
		"http://ravenfs-1:9090",
		"http://ravenfs-2:9090"
	]
}
{CODE-END /}
This document defines a list of server urls - one url per each instance that you want to synchronize. Adding such a configuration automatically enables synchronization to the given servers. If you want to stop syncing to any server just drop its url, in order to turn the synchronization feature off just remove the config. 

##Work model

The synchronization in RavenFS can work in *master/slave* model (one instance is a destination for the second instance, but not vice versa), then we are dealing with one-way synchronization. Any changes made to the master server will be propagated to the other servers, but changes made on the slave server will not be reflected on other servers. 

RavenFS also supports *master/master* synchronization scenarios. Only step required to do this is to setup destination servers for each other. Then any file update on any server will be propagated among any other servers (including masters). You need to be aware that applying such a configuration could create conflicts between your files ([read more about conflicts](conflicts)).

##Synchronization steps

Every file modification triggers the synchronization task on the source server in the background. RavenFS looks up the list of synchronization destinations and it takes the following actions for each destination:

1. Query the remote instance for the last file (actually file ETag) that we synchronized to that instance ([see](configurations#ravensynchronizationsourcessource-server-id) `Raven/Replication/Sources/[server-id]` config)
2. For each file that has changed since the last synchronization (based on returned ETag value):
	* download file metadata from the destination server
	* use this metadata to determine what kind of a synchronization type is needed (see [Synchronization types](synchronization-types))
	* add an appropriate synchronization work to a pending synchronization queue
3. Perform as many synchronizations as you can to the destination (RavenFS limits the number of the simultaneous synchronizations to the same destination, [see](configuration#raven-synchronization-limit) `Raven-Synchronization-Limit` config). 

{INFO According to the determined synchronization type, different data will be sent to the destination server. /}

{INFO Every finishing synchronization work always checks if there is any pending work that it could run. /}

##File lock and timeout

A destination server during synchronization process denies to perform any operation on the syncing file. Any attempt to modify a file will result `PreconditionFailed (412)` response. The file locking mechanism creates a configuration `SyncingLock-[filename]` at the very beginning of the synchronization and as long as it exists, the file is not accessible. It is removed at the end of the file synchronization process. 

In order to avoid potential deadlocks (e.g. when server restarts in the middle of the synchronization) there is also a timeout mechanism. You can control its value that by specifying in `Raven-Synchronization-Lock-Timeout` configuration. If the timeout in locking the file is exceeded, any attempt to access the file will automatically unlock it. By default the synchronization has the timeout of 10 minutes.

{INFO Checking whether a file is already locked and a lock operation are made in a single transaction. There is no option that two servers will synchronize the file with the same name to the same destination server. The first one will lock the file what will prevent the second one to perform any action. /}

##Synchronization aborting

In contrast to a destination server, a source server does not lock a file during the synchronization. You are able to work with it, because it is just read and sent to a destination. However if you modify the file in the middle of synchronization, it will abort the active synchronization and will try with a new version.

##Handling failures and restarts

RavenFS has been designated to work with large files. By nature performing some synchronizations can take quite long time. Especially in the case when a new file has been uploaded and then the server has to transfer the entire file to other nodes.
This makes that possible synchronization failures might be caused by network problems or restarts of destination servers. Also the destination server can go down for a long period of time. 

RavenFS ensures that every file change will be reflected on the destination even though any failure has occured or the remote server has been not available. In order to make sure that no update was missed it uses the following mechanism:

###Tracking last ETag
Every successful synchronization on the destination side stores an ETag number of already synchronized file. If the source determines what files require the synchronization it asks the destination about last ETag seen from that server. 
ETags used in RavenFS are sequential, so we just need to synchronize files with greater ETag that last seen.

###Confirmations
Every synchronization must be confirmed. After a synchronization cycle the source stores what files was synchronized. In next cycle it asks the destination about status of the already acomplished synchronizations. If the synchronization failed the file is synchronized again.

###Periodic running
The synchronization task runs every 10 minutes.
