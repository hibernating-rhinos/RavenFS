#Folders listing

Perform a GET request to retrieve all directories under provided directory name. For example to get all directories in *documents* directory:

{CODE-START:csharp/}
> curl.exe -X GET http://localhost:9090/folders/subdirectories/documents
{CODE-END /}

In result you will get a JSON list:
{CODE-START:json/}
["/movies","/music","/pictures"]
{CODE-END /}