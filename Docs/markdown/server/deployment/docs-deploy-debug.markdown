#Running in Debug mode
RavenFS can be deployed in several ways, but the simplest method is to simply start the server located in the release zip under "/Server/RavenFS.Server.exe"
That will start the server as a console application, which displays the server log.

* Close the server by typing "q" and then enter on the console.
* If you want to clear the log and keep the server running, you can type "cls" and then enter.
* If you want to force garbage collection, you can type "gc" and then enter.

Running in this configuration is useful when you just want to try things out, for production permanent deployment, it is recommended to use the Service or IIS modes.