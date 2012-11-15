#Metadata update

RavenFS is able to detect that only file metadata has changed since a last synchronization. Then the source server generate POST message and places source file's metadata as
request headers. The destination server has `/synchronization/updateMetadata` endpoint that allows to change file metadata. The sent metadata will override all metadata values except `ETag` and `Last-Modified` which will get new ones after each synchronization operation.