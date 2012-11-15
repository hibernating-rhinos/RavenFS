#Configuration

RavenFS has a concept of configuration to save not binary data items. In the Client API a config data is a `NameValueCollection` object (the same like file's metadata). 
The client has a basic  methods to access and manage configuration items. All methods you will find under `RavenFileSystemClient.Config` property.

##Create

A configuration must have a name and value. If you want to override an existing one just create a new config and save by using the same name.

{CODE-START:csharp/}
client.Config.SetConfig("WindowSettings", new NameValueCollection()
					                    {
						                    {"Width", "1024"},
						                    {"Height", "768"}
					                    });
{CODE-END /}

##Read

Provide a configuration name to retrieve it:

{CODE-START:csharp/}
NameValueCollection settings = await client.Config.GetConfig("WindowSettings");
{CODE-END /}

If the provided configuration name does not exists on the server `null` will be returned.

##Delete

Use the following method to delete a configuration:

{CODE-START:csharp/}
await client.Config.DeleteConfig("Settings");
{CODE-END /}

If the config does not exists nothing will happen.

##Existing configurations

In order to check what configurations are present on the server from the client use the code:

{CODE-START:csharp/}
string[] configurations = await client.Config.GetConfigNames();
{CODE-END /}