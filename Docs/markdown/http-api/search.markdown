#Search

##Terms

To see all available search terms use the following GET request:

{CODE-START:csharp/}
> curl -X GET http://localhost:9090/search/terms
{CODE-END /}

As a result you will get a JSON list of strings, that you can use to build a search query.

##Results

### Fetching

If you want to look for files that fulfill specified criteria you can query the file system. 
You can pass any valid Lucene query using the query parameter on the query string. You can read more on [the query syntax on the Lucene documentation](http://lucene.apache.org/core/old_versioned_docs/versions/3_0_0/queryparsersyntax.html).

Sample query to fetch all files that are present in */pictures* folder and where the author is *James* is defined here:

{CODE-START:csharp/}
> curl -X GET http://localhost:9090/search?query=__directory:/pictures%20AND%20Author:James
{CODE-END /}

In result you will get a list of search results that contains the full information about each file that matched the criteria. For example:

{CODE-START:json/}
{
	"Files":[
	{
		"Name":"/pictures/july.jpg",
		"TotalSize":668235,
		"UploadedSize":668235,
		"HumaneTotalSize":"652.57 KBytes",
		"HumaneUploadedSize":"652.57 KBytes",
		"Metadata":
		{
			"Raven-Synchronization-History":"[]",
			"Raven-Synchronization-Version":"98312",
			"Raven-Synchronization-Source":"3d05a8a7-efa6-4d71-bb0e-0833bc33f150",
			"Author":"James",
			"Last-Modified":"10 Nov 2012 17:00:3.14731 GMT",
			"Content-MD5":"8493bab415d29f6c1577c9c7c22a3c37",
			"ETag":"\"00000000-0000-1300-0000-00000000000c\""
		}
	},
	{
		"Name":"/pictures/february.jpg",
		"TotalSize":1449792,
		"UploadedSize":1449792,
		"HumaneTotalSize":"1.38 MBytes",
		"HumaneUploadedSize":"1.38 MBytes",
		"Metadata":
		{
			"Raven-Synchronization-History":"[]",
			"Raven-Synchronization-Version":"98313",
			"Raven-Synchronization-Source":"3d05a8a7-efa6-4d71-bb0e-0833bc33f150",
			"Author":"James",
			"Last-Modified":"11 Nov 2012 17:00:25.88688 GMT",
			"Content-MD5":"229426e2b2403757038b8ed46599a205",
			"ETag":"\"00000000-0000-1300-0000-00000000000d\""
		}
	}],
	"FileCount":2,
	"Start":0,
	"PageSize":25
}
{CODE-END /}
###Sorting

The search results can be sorted directly by the RavenFS server. In order to do that add *sort* parameter to the query. 

{CODE-START:csharp/}
> curl -X GET http://localhost:9090/search?query=__directory:/pictures%20AND%20Author:James&sort=__key
{CODE-END /}

The query above shows how to sort by file path (*__key*) ascending. We can also reverse the sort order by prefixing `-` in front of the sort field, like thus:

{CODE-START:csharp/}
> curl -X GET http://localhost:9090/search?query=__directory:/pictures%20AND%20Author:James&sort=-__key
{CODE-END /}

###Paging

RavenFS by default pages the results, the default values are the start page number is 0 and the page size is 25. You are capable to control them by specify *start* and *pageSize* parameters.
Here is a sample query with custom paging control:

{CODE-START:csharp/}
> curl -X GET http://localhost:9090/search?query=__directory:/pictures&start=2&pageSize=10
{CODE-END /}
