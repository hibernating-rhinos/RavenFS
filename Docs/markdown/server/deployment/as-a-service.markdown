# Running as a service

RavenFS supports running as a system service, creating its own HTTP server and processing all requests internally.

## Installing the RavenFS service

1. Extract the zip with the build files
2. Go to the Server directory
3. Execute the following command on the command line: <code>RavenFS.Server.exe /install</code>  
    _Note:_ Raven may ask you for administrator privileges while installing the service.

RavenFS is now installed and running as a service.

## Uninstalling the RavenFS service

1. Go to the Server directory
2. Execute the following command on the command line: <code>RavenFS.Server.exe /uninstall</code>

The data storage will not be deleted when the server is uninstalled.

## Server Configuration

Many configuration options are available for tuning RavenFS and fitting it to your needs. See the [Configuration options](../configuration) page for complete info.