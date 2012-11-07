#Metadata update

RavenFS is able to detect that only file metadata has changed since a last synchronization. Then the source server generate POST message and places source file's metadata as
request headers. The destination server has `/synchronization/updateMetadata` endpoint that allows to change file metadata. The sent metadata will override the existing ones except such as `ETag` and `Last-Modified` which will get the new values as always after the synchronization operation.