#HTTP API - File operations

##GET

###Download single file

In order to download a single file and save it locally perform a GET request with a given name of a file:

{CODE-START:csharp/}
> curl -X GET http://localhost:9090/files/filename.bin -o C:\localfilename.bin
{CODE-END /}

If the file exists the following reponse headers you will get:

	HTTP/1.1 200 OK
	Cache-Control: no-cache
	Transfer-Encoding: chunked
	Content-MD5: 5572acd3b4aa8ed88f1a9ac2c6614ce5
	Last-Modified: Mon, 10 Nov 2012 07:00:00 GMT
	ETag: "00000000-0000-0300-0000-000000000002"
	Server: Microsoft-HTTPAPI/2.0
	Content-Disposition: attachment; filename=filename.bin
	Raven-Synchronization-History: []
	Raven-Synchronization-Version: 16385
	Raven-Synchronization-Source: 3d05a8a7-efa6-4d71-bb0e-0833bc33f150
	Sample: Metadata
	Date: Mon, 10 Nov 2012 08:00:00 GMT

Note that headers contain all file metadata. The response body is the content of the file *"filename.bin"*. 

If the specified file does not exist, `HTTP/1.1 404 Not Found` you will be returned.

###Browse files

If the GET request does not contain a file name, you will get the list of the existing files in JSON format:

{CODE-START:csharp/}
> curl -X GET http://localhost:9090/files
{CODE-END /}

Returned results are always paged, you can control paging by specify parameters `start` and `pageSize`, for example if you want to browse 2nd page by using 5 items per page the request URL should look as following:

{CODE-START:csharp/}
> curl -X GET http://localhost:9090/files?start=2&pageSize=5
{CODE-END /}
By default the page size is 25 and it starts browsing from page number 0.

Each item from the returned collection has the information:

* a file name,
* file sizes: total and uploaded,
* file metadata.

##PUT

Perform a PUT request to upload a file that exist on local machine:

{CODE-START:csharp/}
> curl -X PUT http://localhost:9090/files/filename.bin -T C:\localfile.bin
{CODE-END /}

You can also specify a file content directly from command line:

{CODE-START:csharp/}
curl -X PUT http://localhost:9090/files/filename.bin -d "file content goes here"
{CODE-END /}

For a successful request, RavenFS will respond with `HTTP 201 Created` response code.

##PATCH

Use PATCH verb to rename a file. To rename *file.bin* to *newfilename.bin* use the URL address as shown below:

{CODE-START:csharp/}
> curl -X PATCH http://localhost:9090/files/file.bin?rename=newfilename.bin
{CODE-END /}

For a successful rename, RavenFS will respond with an HTTP response code 204 No Content:

`HTTP/1.1 204 No Content`

The move operation in RavenFS is nothing else like just renaming a file. For example:

{CODE-START:csharp/}
curl -X PATCH http://localhost:9090/files/pictures/picture.jpg?rename=wallpapers/picture.jpg
{CODE-END /}

will move *picture.jpg* from *pictures* directory to *wallpapers*.

There are two ways where this operation can fail. The first one is when a file does not exist, then `404 Not Found` will be the result. 
The second case is when "rename" paramteter indicate a file name that already exist in the file system, then `HTTP/1.1 403 Forbidden` will be resulted.

##HEAD

To retrieve only file metadata use a HEAD request:

{CODE-START:csharp/}
> curl -X HEAD http://localhost:9090/files/file.bin
{CODE-END /}

All metadata will become headers of a response for a successful operation `HTTP/1.1 200 OK`.

##POST

Perform a POST request to set new metadata of a file:

{CODE-START:csharp/}
> curl -X POST --header "Content-Length:0" --header "Genre:Pop" --header "Artist:Unknown" http://localhost:9090/music/song.wmv
{CODE-END /}

In this sample metadata *"Genre"* is set to *"Pop"* and *"Artist"* to *"Unknown"*. All metadata are sent in request headers, note that *Content-Length* is *0*.

##DELETE

In order to delete a file perform a DELETE request:

{CODE-START:csharp/}
> curl -X DELETE http://localhost:9090/file.bin
{CODE-END /}

For a successful delete, RavenFS will respond with an HTTP response code 204 No Content:

`HTTP/1.1 204 No Content`

