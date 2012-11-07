# Configuration options

All the configuration options detailed below are defined in the <appSettings> section of your config file as separate values. When running RavenFS as a website (IIS), the config file is web.config; otherwise it is the RavenFS.Server.exe.config file.

For your changes to be recognized you will need to restart the service. You can do so calling: <code>RavenFS.Server.exe /restart</code>.

If RavenFS is running as an IIS application, touching the web.config file will cause IIS to automatically restart RavenFS.

## Sample configurations file

This is the standard app.config XML file. The `appSettings` section is where the configuration options go, also for web applications which have a web.config file instead.

{CODE-START:xml /}
<?xml version="1.0" encoding="utf-8" ?> 
<configuration> 
  <appSettings> 
    <add key="Raven/Port" value="*"/> 
    <add key="Raven/DataDir" value="~\Data.ravenfs"/>
  </appSettings> 
        <runtime> 
                <loadFromRemoteSources enabled="true"/> 
                <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1"> 
                        <probing privatePath="Analyzers"/> 
                </assemblyBinding> 
        </runtime> 
</configuration>
{CODE-END /}

## Index settings

* **Raven/IndexStoragePath**  
    The path to the indexes that are kept on disk. Putting them in a different drive than the actual data will improve performance significantly.  
    _Default_: ~/Data/Indexes
## Data settings:

* **Raven/DataDir**  
    The directory for the RavenDB database. You can use the ~\ prefix to refer to RavenDB's base directory.  
    _Default:_ ~\Data  

## Http settings

* **Raven/HostName**  
    The hostname to use when creating the http listener (null to accept any hostname or address)  
    _Default:_ none, binds to all host names  

* **Raven/Port**
    The port to use when creating the http listener.  
    _Default:_ 8080  

* **Raven/VirtualDirectory**  
    The virtual directory to use when creating the http listener.  
    _Default:_ /  

## Esent settings

* **Raven/Esent/CacheSizeMax**  
    The maximum size of the in memory cache that is used by the storage engine. The value is in megabytes.  
    _Default:_ 1024  

* **Raven/Esent/MaxVerPages**  
    The maximum size of version store (in memory modified data) available. The value is in megabytes.  
    _Default:_ 128  

* **Raven/Esent/DbExtensionSize**  
    The size that the database file will be enlarged with when the file is full. The value is in megabytes. Lower values will result in smaller file size, but slower performance.  
    _Default:_ 16  

* **Raven/Esent/LogFileSize**  
    The size of the database log file. The value is in megabytes.  
    _Default:_ 64  

* **Raven/Esent/LogBuffers**  
    The size of the in memory buffer for transaction log.  
    _Default:_ 16  

* **Raven/Esent/MaxCursors**  
    The maximum number of cursors allowed concurrently.  
    _Default:_ 2048  
    
* **Raven/Esent/LogsPath**  //TODO
    Where to keep the Esent transaction logs. Putting the logs in a different drive than the data and indexes will improve performance significantly.  
    _Default_: ~/Data/logs  

* **Raven/Esent/CircularLog** //TODO 
    Whether or not to enable circular logging with Esent.  
    _Default_: true  
