#Metadata

The Client API exposes methods to work with file metadata without downloading the entire content. You are able to download file metadata by using the code:

{CODE-START:csharp/}
NameValueCollection metadata = await client.GetMetadataForAsync("file.bin");
{CODE-END /}

It the file does not exists on the server, returned metadata will be a `null` value.

To change your metadata use:
{CODE-START:csharp/}
client.UpdateMetadataAsync("directory/file.bin", new NameValueCollection()
												{
													{"NewMetadata", "Value"}
												});
{CODE-END /}

Note that if you upload a new metadata the old one will be overrided. If you want to just edit you should update previously downloaded metadata, edit it and pass to `UpdateMetadataAsync` method.