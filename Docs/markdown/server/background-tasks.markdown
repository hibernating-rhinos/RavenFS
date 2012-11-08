#Background tasks

##File delete and rename operations

RavenFS supports resuming of delete and rename operations when either it could not finish it successfully or the server has been restarted in the middle. If any of these operations is applied to a really large file we have to be aware that it might take longer period of time.
In order to know that the operation has been initiated RavenFS creates the following configurations: *DeleteOp-[filename]* and *RenameOp-[filename]*. They are deleted only if the operation succeeded. In the background of RavenFS server
there are two tasks that detect if any file operation requires to be resumed. If there are any the server retries to finish it. The rename and delete tasks are run every 15 minutes.

##Synchronization

Another RavenFS work that is run in the background is a synchronization. It is perfomed periodically to ensure that all modifications are propagated to destinations, even though there were so many file updates that the server could not execute them at once.
Such a case is very likely when any destination server was down for long period of time while on a source server there were a lot of file system changes. When the destination wakes up the source will try to synchronize the whole bunch of missing updates.
The synchronization task is run every 10 minutes.