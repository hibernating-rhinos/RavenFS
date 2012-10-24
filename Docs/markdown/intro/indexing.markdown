## Indexing

RavenFS allows you to look for files by using [Lucene query syntax](http://lucene.apache.org/core/old_versioned_docs/versions/3_0_0/queryparsersyntax.html). You can search a file by using:

* name
* size
* directory
* date of modification
* any metadata.

The more files and corresponed metadata you add the more search terms you can use to build your search query. All available search fields you will find under `search/terms` endpoint. Here is an explanation of built-in search fields: 
(let's assume that we have a file *documents/pictures/wallpaper.jpg*)

* __key - the full name of the file (*"documents/pictures/wallpaper.jpg"*),
* __fileName - the last part of file path (*"wallpaper.jpg"*),
* __rfileName - the reversed version of the _fileName, it allows searches that ends with wildcards (*"gpj.repapllaw"*),
* __directory - the list of directories associated with the file (*"/documents/pictures"*, *"/documents"*, *"/"*),
* __level - the nesting level (3),
* __modified - the date of indexing (the date index format is *yyyy-MM-dd_HH-mm-ss*),
* __size - the file length (in bytes) stored as string,
* __size_numeric - the file length (in bytes) stored as numeric fields, what allows to search by range.

A sample query to find all files under */documents* directory (or nested) that name ends with *.jpg* and size is greater than 1MB:

`__directory:/documents AND __rfileName:gpj.*  AND __size_numeric:[1048576 TO *]`

The search supports sorting as well (ascending and descending), the possible options of sorting are:

* by name,
* by size,
* by date of modification.

The easiest way to search for files from the code is to use either Client API or HTTP API.

Searching is also supported by The Studio, where you will find usefull predefined search filters:

![Figure 1: Search filters](images\studio-search-filters.png)
