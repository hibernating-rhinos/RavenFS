#Connecting to server

No matter which deplyment options has been chosen to run RavenFS server instance (IIS or Windows service), the only thing that the client needs to know is the server address.
In order to connect to the server use the following code:

{CODE-START:csharp/}
using (var client = new RavenFileSystemClient("http://localhost:9090"))
{
				
}
{CODE-END /}

The important thing to note is that `RavenFileSystemClient` implements `IDisposable` interface, so you need to remember about disposing it.
