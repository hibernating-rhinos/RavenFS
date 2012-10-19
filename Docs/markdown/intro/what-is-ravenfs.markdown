# What is RavenFS?

RavenFS is a distributed file system designated for large files handling. It offers a transactional data storage, supports a file indexing and an efficient synchronization accross WAN by minimizing transferred data.

RavenFS is built on a client-server architecture. A server is a HTTP Web Server, responsible for storing and serving files as well as the synchronization of file updates to slibing servers. A client part communicates with the server by generating HTTP request. There are two possible ways to achieve the server's reponse, by using [Client API](../client-api) available to any .NET or Silverlight application, or by [directly accessing the server's RESTful API](../http-api).

## Basic concepts

Files

Configuration

Searching

Synchronization

## Files management

You can easily manage your files by using a built-in Silverlight application - The Studio. In order to access it hit the server url address (and the port) in a web browser. More details about how to use The Studio you will find here.

// TODO paste studio image