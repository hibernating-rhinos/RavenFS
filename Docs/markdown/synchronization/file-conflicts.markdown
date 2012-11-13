#File conflicts

When working with a distributed and synchronized file system such as RavenFS, you have to be aware of possible conflict situations. Each file in RavenFS has a version and history (stored as file metadata). 
Every time you modify the file its version is changed and then the old value is moved to the history. 

The conflict detection mechanism works based on the mentioned metadata. The conflict between two files with the same name will occur if they have two independent versions. 
Such a case is achievable when two files with the same name was uploaded to two different servers. From RavenFS perspective those are two completly different files, so they cannot override each other.

##History role in synchronization
If a file that has been created on *Server A* is synchronized to *Server B* it is transferred with its metadata. What results that files on both servers have the same version and history. 
Any file update on *Server B* results in creating a new file version, but the old version goes to the history. Now if *Server B* wants to synchronize the file to *Server A* there is no conflict because the file version from *Server A*
is contained in the history of the file from *Server B*.

{INFO If a file on destination server is an acestor of a file from source server there is no conflict and a file update can be applied. /}

The history and versioning mechanisms make an easy way to track what servers made changes on the file and detect the conflict.

##Conflict items
If a conflict is detected an apropriate configuration item is created ([see](configuration#conflicted-filename) `Conflicted-[filename]`) on the destination server. The full list of conflicted files you can retrieve from `/synchronization/conflicts` endpoint.

##Detection side

Generally conflicts are detected on the destination server, but in case of RavenFS which is designated to work with large files we do it on the source server based on file metadata retrieved from the destination.
This small optimization step prevent the source server to transfer the entire file just to know that it is conflicted. Hence in RavenFS implemented synchronization algorithm first checks conflict and remotely create a conflict item on destination if needed.

##Conflicts resolution
We can resolve a conflict by using one of the following strategies:

* Current - take a file that exists on the destination,
* Remote - take a file version from the source server.

If RavenFS servers work in master/master model, after applying any of those strategies we can be sure than an apropriate version the the file will be synchronized in the next synchronization cycle.
It we setup master/slave format, the resolution by *Remote* will synchronize the file from the source as well, but if we resolve by using *Current* strategy we have to manually invoke synchronization to the source server.

More details about dealing with conflicts from the Client API you can find [here](../client-api/synchronization-client#conflicts).
