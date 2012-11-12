#Managing configurations

##GET

###Browse configs

To check all saved configuration items use the URL:

{CODE-START:csharp/}
> curl -X GET  http://localhost:9090/config
{CODE-END /}

In result you will get a JSON list of configuration names, e.g.:

{CODE-START:json/}
["Raven/Sequences/Raven/Etag","Raven/Synchronization/VersionHilo"]
{CODE-END /}

###Download config

Execute the following request to get a configuration named *WindowSettings* as JSON:

{CODE-START:csharp/}
> curl -X GET  http://localhost:9090/config/WindowSettings
{CODE-END /}

A response with a HTTP status `HTTP/1.1 200 OK` should be returned. If config does not exist `HTTP/1.1 204 No Content` will be the result status.

##PUT

To create a configuration item *WindowSettings* create the following request:
{CODE-START:csharp/}
> curl.exe -X PUT  http://localhost:9090/config/WindowSettings -d "{'Width':1024, 'Hight':768}"
{CODE-END /}

After a successful PUT a response status is `HTTP/1.1 201 CREATED`.

##DELETE

In order to delete a configuration perform a DELETE request:

{CODE-START:csharp/}
> curl -X DELETE http://localhost:9090/config/ConfigurationName
{CODE-END /}

For a successful delete, RavenFS will respond with an HTTP response code 204 No Content:

`HTTP/1.1 204 No Content`