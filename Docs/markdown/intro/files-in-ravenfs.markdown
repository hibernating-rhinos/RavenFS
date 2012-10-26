# Files in RavenFS

RavenFS stores data in Esent ([Extensible Storage Engine](http://msdn.microsoft.com/en-us/library/windows/desktop/gg269259%28v=exchg.10%29.aspx)). It's a native, built-in Windows database engine that supports transactions and ensures a data consistency. The default data directory is *~/Data.ravenfs*.

## What is a file?

A file in RavenFS consists of:

* name (full path),
* total size,
* uploaded size,
* collection of key/value properties - metadata,
* sequence of bytes that make up the file content.

## Default metadata

A file has an associated collection of properties called metadata. A user can attach any information about the file as another metadata item. There are also some predefined properties that each file in RavenFS must have. They are handled by the server internally. Here is sample default file metadata:

{CODE-START:json /}
{
	ETag: ""00000000-0000-0100-0000-000000000004"",
	Last-Modified: "20 Oct 2012 10:20:30.79023 UTC",
	Content-MD5: "01ef0c197d5673839eb252e233f322d4",
	Raven-Synchronization-Version: "2",
	Raven-Synchronization-Source: "ac834c68-f018-4640-81ba-3813bdffaca6",
	Raven-Synchronization-History: "[{"Version":1, "ServerId":"ac834c68-f018-4640-81ba-3813bdffaca6"}]"
}
{CODE-END /}

* *ETag* is a file identifier, updated every time if the file is modified. The file is considered as modified when a new version was uploaded, the file name or metadata was changed or any of those updates was synchronized from a remote server.
* *Last-Modified* is the date of the last file modification, note that RavenFS uses Coordinated Universal Time (UTC). 
* *Content-MD5* is a hash of a file content, calculated on the fly during a file upload, by using MD5 algorithm.
* *Raven-Synchronization-Version* is a number describing a file version on the server.
* *Raven-Synchronization-Source* is an unique identifier of the origin server (where a file was changed last time).
* *Raven-Synchronization-History* is a list that consists of *{Version, ServerId}* pairs, where *Version* is a previous *Raven-Synchronization-Version* value and *ServerId* is a previous *Raven-Synchronization-Source* identifier.

{INFO *Raven-Synchronization-Version*, *Raven-Synchronization-Source* and *Raven-Synchronization-History* are always updated together when a file is uploaded or metadata changed. Then the existing *Raven-Synchronization-Version, Raven-Synchronization-Source* values are added as a new history item to the *Raven-Synchronization-History* list and new values are assigned to them. All of those propetries, according to their names, are utilized for synchronization purposes./}

## Directories
	
In RavenFS there are no physical directories. The directory tree is built upon names of existing files. The file name must be a full path e.g. *Documents/Pictures/wallpaper.jpg*. RavenFS groups the files into the directories just virtually. Hence a move operation performed on the file is actually a renaming.

## Pages

Internally each file is divided into pages. The page is a sequence of bytes, its maximum size is 64KB and it has an unique identifier (a pair of hashes from a page content). The concept of pages implicates the following:

* stored pages are unique
* file content is an ordered list of page references
* each page might be a part of multiple files
* pages are imutable, once they are written to storage, they cannot be modified (but they can be removed if no file is referencing this page)
* taken disk space is reduced by reusing pages if files share the same information