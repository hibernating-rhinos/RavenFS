#Content update

There are two modifications of the file system that are considered as file content updates:

* upload a new file,
* upload a file with changed content.

In the first case the matter is obvious, we need to transfer entire file. In the second one we use much more efficient approach.

##How to detect that content has changed?

Every time that we upload a file RavenFS calculates its hash on the fly. It does it by using MD5 algorithm and next stores it in metadata as'Content-MD5'.
So we can easily determine if contents of the files are different on both servers just by compare those metadata.

##Remote Differential Compression