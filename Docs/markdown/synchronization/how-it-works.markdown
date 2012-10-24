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
This document defines a list of server urls - one url per each instance that you want to synchronize. Adding such a configuration automatically enable the synchronization to the given servers. If you want to stop syncing to any server just drop its url, in order to turn the synchronization feature off just remove the config. 

##Work model

The synchronization in RavenFS can work in *master/slave* model (one instance is a destination for the second instance, but not vice versa), then we are dealing with one-way synchronization. Any changes made to the master server will be propagated to the other servers, but changes made on the slave server will not be reflected on other servers. 

RavenFS also supports *master/master* synchronization scenarios. Only need to do is configuring RavenFS instances as destination servers for each other. Then any file updates to any of the master server will be synchronized to others (that also are masters). You need to be aware that applying such a configuration could make conflicts between your files ([read more about conflicts](conflicts)).

##Synchronization steps

Every file modification triggers the synchronization task on the source server in the background. RavenFS looks up the list of synchronization destination. For each of destinations, the task will take the following actions:

1. Query the remote instance for the last file (actually file ETag) that we synchronized to that instance (see `Raven/Replication/Sources/[server-id]` config)
2. For each file that has changed since the last synchronization (based on returned ETag value):
	* download file metadata from the destination server
	* use this metadata to determine what kind of a synchronization type is needed (see [Synchronization types](synchronization-types))
	* add an apropriate synchronization work to a pending synchronization queue
3. Perform as many synchronizations as you can to the destination (RavenFS limits the number of the simultaneous synchronizations to the same destination, see `Raven-Synchronization-Limit` config). 

{INFO According to the synchronization type different kind of data will be sent to the destination server. /}

{INFO Every finishing synchronization work always checks if there is any pending work that it could run. /}

##File lock and timeout

A destination server during synchronization process denies to perform on the syncing file any operation. If you try to modify a file while the file is synchronized, you will get `PreconditionFailed - 412 HttpStatus` response. Internally the server creates a configuration `SyncingLock-[filename]`, and as long as it exists, the file is not available to use. In order to avoid potential deadlocks (e.g. when server restart in the middle of synchronization), there is `Raven-Synchronization-Timeout` configuration. If a synchronization lasts longer that the specified timeout, the file lock configuration is removed by any attempt to access the file.

##Synchronization aborting

In contrast to a destination server, a source server does not lock file during synchronization. You are able to normally work with it. However if you modify a file in the middle of synchronization, it will abort an active synchronization and will try with a new version. 

##What about failures and restarts?

