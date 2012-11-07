# Deploying as an IIS application

RavenFS can be run as an IIS application, or from a virtual directory under an IIS application.

## Setting up a RavenFS IIS application

1. [Download the distribution zip](http://ravenfs.net/download), and extract the "Web" folder.
2. In IIS Manager, create a new website and point it's physical path to the `"/Web"` folder you extracted. Alternatively, point a virtual directory under an existing website to that folder.
3. Set the Application Pool for the IIS application you will be using to "ASP.Net v4.0", or create a new Application Pool set to .NET 4.0 Integrated Pipeline.
4. Set port and host if needed.
5. Make sure that the user you set for the website has write access to the physical database location.
6. Make sure to disable "Overlapped Recycle" in App Pool Advanced Settings.  (Otherwise, you may have two concurrent RavenFS instances competing for the same data directory, which is going to generate failures).

## Web Configuration

Many configuration options are available for tuning RavenFS and fitting it to your needs. See the [Configuration options](../configuration) page for complete info.

## Recommended IIS Configuration

RavenFS isn't a typical web site because it needs to be running at all times. In IIS 7.5, you can set this using the following configuration settings:

* If you created a dedicated application pool for RavenFS, change the application pool configuration in the application host file (C:\Windows\System32\inetsrv\config\applicationHost.config) to:

{CODE-START:csharp/}
       <add name="RavenFSApplicationPool" managedRuntimeVersion="v4.0" startMode="AlwaysRunning" />
{CODE-END/}

* If RavenFS runs in an application pool with other sites, modify the application host file (C:\Windows\System32\inetsrv\config\applicationHost.config) to: 

{CODE-START:csharp/}
       <application path="/RavenFS" serviceAutoStartEnabled="true" />
{CODE-END/}