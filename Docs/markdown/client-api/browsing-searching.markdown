##Browsing and searching

##Browse all files

The simplest way to browse files is to use `BrowseAsync` method. It will list all files stored on the server, just note the built-in paging functionality.

{CODE-START:csharp/}
using (var client = new RavenFileSystemClient("http://localhost:9090"))
{
	foreach (var fileInfo in await client.BrowseAsync(start:0, pageSize:20))
	{
		Console.WriteLine("File: {0}, size: {1} ({2} bytes)", 
			fileInfo.Name, fileInfo.HumaneTotalSize, fileInfo.TotalSize);
	}
}
{CODE-END /}

Each item from returned array of `FileInfo` contain:

* a full path of a file,
* a size in a human readable format (e.g. 10GBytes),
* a size in bytes.

##Search

Most of cases you will be rather interested in searching than just browsing the file system. Take a look at [the indexing chapter](../intro/indexing) for more theory details.
The Client API exposes `SearchAsync` method to look for files. 

First let's upload some sample empty files with defined metadata:

{CODE-START:csharp/}
using (var client = new RavenFileSystemClient("http://localhost:9090"))
{
	await client.UploadAsync("3.mp3", new NameValueCollection() {{"Genre", "Rock"}}, 
																	new MemoryStream());
	await client.UploadAsync("2.mp3", new NameValueCollection() {{"Genre", "Classic"}}, 
																	new MemoryStream());
	await client.UploadAsync("/music/1.mp3", new NameValueCollection() {{"Genre", "Rock"}},	
																	new MemoryStream());
}
{CODE-END /}

In order to get all files that have metadata *Genre* set to *Rock* use the following code:

{CODE-START:csharp/}
SearchResults searchResult = 
	await client.SearchAsync("__directory:/ AND Genre:Rock");
{CODE-END /}

As result you will get: *3.mp3* and */music/1.mp3*.

If you want to look _only_ in main */*  directory limit the search nesting level by adding ___level_ term:

{CODE-START:csharp/}
SearchResults searchResult = 
	await client.SearchAsync("__directory:/ AND Genre:Rock AND __level:1");
{CODE-END /}

###Search Terms

To retrieve all available search terms that you can use to look for files use the instruction:

{CODE-START:csharp/}
string[] searchTerms = await client.GetSearchFieldsAsync();
{CODE-END /}

In our sample scenario the result will look as following:
	"__key",
    "__fileName",
    "__rfileName",
    "__directory",
    "__modified",
    "__level",
    "Genre",
    "Last-Modified",
    "ETag",
    "Raven-Synchronization-History",
    "Raven-Synchronization-Version",
    "Raven-Synchronization-Source",
    "__size",
    "__size_numeric",
    "Content-MD5",
    "Content-Length"

There are all default search terms such as ___key_ , ___filename_ etc. but also all metadata keys of all files like *Genre* or *ETag*.

###Sorting

Sorting can be done internally by RavenFS. To achieve that indicate in 3rd parameter of `SearchAsync` method the sort fields. The following values can be use to sort results:

* `__key` (by file name),
* `__size` (by file size),
* `__modified` (by date of modification).

By default the results will be sorted ascending, if you want to sort in descending order add "`-`" at the beginning of the term. For example the query:

{CODE-START:csharp/}
SearchResults searchResult = 
	await client.SearchAsync("__directory:/", new[] {"-__key"});
{CODE-END /}

will result in the order: *3.mp3*, *2.mp3* and */music/1.mp3*. Note that you can pass more than one sort field.