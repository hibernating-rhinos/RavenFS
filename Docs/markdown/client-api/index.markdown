# Client API

The Client API is the easiest way to access and manage files stored in Raven File System from any .NET application. There are two separate client DLLs designed to use with standard .NET applications (RavenFS.Client) and for Silverlight usages (RavenFS.Client.Silverlight). 
Both implement the same functionalities and provide identical API. The entire API is designed to work in async manner. The Client API methods always return `Task` or `Task<T>`. If you are going to use the client in .NET 4.5 application the best way it to use `async/await` keywords to prevent a blocking of a main application thread.
When you are coding .NET 4 or Silverlight 5 app consider to use [`AsyncTargetingPack`](http://nuget.org/packages/Microsoft.CompilerServices.AsyncTargetingPack) to get the same experience as in .NET 4.5.

##What is the Client API?

The communication between a client and a server is made via HTTP. The Client API is actually a HTTP requests generator to the RavenFS server instance.
The Client API also takes care of deserializing a server's response formatted as JSON into .NET object.

