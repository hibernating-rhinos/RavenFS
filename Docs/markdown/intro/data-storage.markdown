# Data storage

RavenFS stores files in Esent (Extensible Storage Engine). It's a native, transactional data store, ensuring data consistency and it's built-in Windows.

## File in RavenFS

A file in RavenFS consists of:

* a name
* a total size
* an uploaded size
* a set of key/value properties - metadata
* a sequence of bytes that make up the file content


## Pages

Internally each file is divided into pages. The page is a sequence of bytes, its maximum size is 64KB and it has an unique identifier (a pair of hashes of the page content). The concept of pages implicates the following:

* stored pages are unique
* a file content is an ordered list of page references
* each page might be a part of multiple files
* pages are imutable, once they are written to storage, they cannot be modified (but they can be removed if no file is referencing this page)
* the size taken by files that share much of the same information is reduced
