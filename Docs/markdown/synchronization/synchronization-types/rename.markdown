#Rename

RavenFS is able to recognize that a file was renamed and in order to synchronize destinations it doesn't need to transfer entire file. The rename synchronization is basically executed by sending an instruction to the destination servers
to rename the file on their side. The rename endpoint on a destination server is `/synchronization/rename`.