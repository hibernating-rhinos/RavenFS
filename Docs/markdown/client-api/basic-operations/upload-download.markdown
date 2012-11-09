#Upload and download

##Upload

To upload a file to RavenFS server you have to provide:

* a name of the file,
* content,
* optionally custom metadata.

The following code shows up how to save a file:
{CODE-START:csharp/}
using (var client = new RavenFileSystemClient("http://localhost:9090"))
{
	using (var content = new FileStream("C:\\file.bin", FileMode.Open))
	{
		var metadata = new NameValueCollection
						    {
							    {"User", "John"}
						    };

		await client.UploadAsync("documents/file.bin", metadata, content);
	}
}
{CODE-END /}

There are a few things to notice in this example:

* file name is the full path,
* metadata is stored in code as `NameValueCollection` which can have a multiple the same key,
* file content passing as last argument can be any readable `Stream`.

##Download

In order to get the file from RavenFS we use `DownloadAsync` method:

{CODE-START:csharp/}
using (var client = new RavenFileSystemClient("http://localhost:9090"))
{
	using (var stream = new MemoryStream())
	{
		NameValueCollection metadata = await client.DownloadAsync("directory/file.bin", stream);
	}
}
{CODE-END /}

In the example above when aync download finishes, file content will be loaded into a stream (memory in this case) and as result of this operation file metadata are returned.

##Partial download

RavenFS allows to do partial downloads. The client's method has an optional parameters that you can use to specify what range of the file content you need to fetch.
To achieve first 200 bytes of the file type the following code:
{CODE-START:csharp/}
client.DownloadAsync("directory/file.bin", stream, from: 0, to:200);
{CODE-END /}

