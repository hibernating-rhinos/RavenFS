# What is RavenFS?

RavenFS is a distributed file system designated for large files handling. It offers a transactional data storage, supports a file indexing and an efficient synchronization accross WAN by minimizing the amount of transferred data.

RavenFS is built on a client-server architecture. A server is a HTTP Web Server, responsible for storing and serving files as well as the synchronization of file updates to slibing servers. A client part communicates with the server by generating HTTP requests. There are two possible ways to perform an operation on the server - by using [Client API](../client-api) available to any .NET or Silverlight application, or by [directly accessing the server's RESTful API](../http-api). A server's response is always formatted as JSON.

## Basic concepts

### File

An essential item that you work with is a file. Beside binary data that make up file content, each file has associated metadata. There are some metadata that every file always has because they are added by RavenFS and used internally (e.g. _ETag_), however you can add your own to attach to a file any useful information. More details about how does RavenFS stores files internally you will find in the chapter [Files in RavenFS](files-in-ravenfs).

### Configuration

A configuration is an item for keeping non-binary data as a collection of key/value properties. Every configuration has a unique name. RavenFS uses configurations internally (e.g. _Raven/Synchronization/Timeout_) to store some domestic data but you are also able to create and manage your owns.

### Indexing

Files in RavenFS are indexed by default. It allows you to execute the queries against stored files. For indexing RavenFS uses Lucene search engine library, what allows you for an efficient search by using a file name, a size and metadata.

### Synchronization

RavenFS offers a great feature of a file synchronization between other RavenFS servers. It works out of the box, the only think you need to do is to provide the list of destination servers. The synchronization starts if any of the following file operation has occured:

* new file uploaded,
* file content changed,
* file renamed,
* file deleted,
* metadata changed.

It is also run periodically to handle failures and restart scenarios. Each of the above operation is related with a different kind of synchronization work, in order to minimize the cost of transferred data across WAN. For example if you just modify a file content, only the file chunks that was changed will be sent across a network. In order to get more details about implemented synchronization solutions [click here](../synchronization/how-it-works).

## Files management

You can easily manage your files by using a built-in Silverlight application - The Studio. In order to access it hit the server url address (and the port) in a web browser. More details about how to use The Studio you will find here.

![Figure 1: The Studio UI](images\studio-main.png)