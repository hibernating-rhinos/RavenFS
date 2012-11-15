##Synchronization configurations

RavenFS uses configuration items during a file synchronization process. This section describes their meaning and format.

##Raven/Synchronization/VersionHilo

Used by HiLo algorithm to store *"Hi"* value. HiLo method is used to generate a file version saved in [metadata](../intro/files-in-ravenfs#default-metadata).

{CODE-START:json /}
{
	"value":"1"
}
{CODE-END /}

##Raven/Synchronization/Sources/[source-server-id]

The configuration stored on a destination server after a successful file synchronization, with ETag *LastSourceFileEtag*. There is one configuration per a source server instance.

{CODE-START:json /}
{
    "LastSourceFileEtag": "00000000-0000-0100-0000-000000000002",
    "SourceServerUrl": "http://ravenfs:9090/",
    "DestinationServerId": "edc0b84c-6737-4a19-8821-529a8e523dad"
}
{CODE-END /}

##SyncingLock-[filename]

Its existence causes that the file `[filename]` is locked. It exists only if the file is being synchronized.

{CODE-START:json /}
{
    "SourceServer": "{
						\"Url\":\"http://ravenfs:9090/\",
						\"Id\":\"2fe55b6b-5506-4945-b89e-c8c5e5e3171e\"
					}",
    "FileLockedAt": "10/20/2012 10:20:30 AM"
}
{CODE-END /}

##Raven-Synchronization-Lock-Timeout

It allows to control the default locking timeout during synchronization (if it does not exist the default value is *10 minutes*). Value format is *hh:mm:ss*.

{CODE-START:json /}
{
    "value": "\"00:01:00\""
}
{CODE-END /}

##Raven-Synchronization-Limit

It limits the number of concurrent file synchronizations to the same destination server (if it does not exists the default value is *5*).

{CODE-START:json /}
{
	"value":"1"
}
{CODE-END /}

##Syncing-[destination-server-url]-[filename]

This configuration is stored on a source server for every already synchronized file to a destination. It is removed if the destination confirms that the synchronization succeeded.

{CODE-START:json /}
{
    "FileName": "foo.avi",
    "FileETag": "00000000-0000-0100-0000-000000000002",
    "DestinationUrl": "http://ravenfs:9090",
    "Type": "ContentUpdate"
}
{CODE-END /}

##SyncResult-[filename]

This configuration represents a result of the synchronization of the `[filename]` file. It's saved by destination server. If any exception was thrown during synchronization, it is stored in *Exception* property. 
*BytesTransfered, BytesCopied and NeedListLength* are filled up only if [the content has changed](synchronization-types).

{CODE-START:json /}
{
    "FileName": "bar.bin",
    "FileETag": "00000000-0000-0100-0000-000000000002",
    "BytesTransfered": "1073741824",
    "BytesCopied": "357913941",
    "NeedListLength": "5",
    "Exception": "",
    "Type": "ContentUpdate"
}
{CODE-END /}

##Conflicted-[filename]

A conflict item is stored as a configuration. It contains histories of both conflicted files, a file name and a url of source server. The conflict is always created on a destination server.

{CODE-START:json /}
{
	"RemoteHistory":"[
						{\"Version\":1,\"ServerId\":\"e3f140ed-68c7-4de0-a772-5dbeeeec7b69\"}
					]",
	"CurrentHistory":"[
						{\"Version\":1,\"ServerId\":\"da6df02b-32b0-4802-bd86-643ca9eb8ee0\"}
					]",
	"FileName":"document.txt",
	"RemoteServerUrl":"http://ravenfs:9090/"
}
{CODE-END /}