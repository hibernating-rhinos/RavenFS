## Synchronization types

The synchronization is triggered by any file modification. RavenFS determines the type of the synchronization by compare file metadata from both servers. This approach allows to send only the necessary amount of data across the network in the synchronization process.

{FILES-LIST /}