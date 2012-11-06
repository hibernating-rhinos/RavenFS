#Monitoring

RavenFS exposes endpoints that allow you to track the current status of the synchronization.

##Source side

RavenFS limits the number of executed synchronizations to the same destination at the same time. Every synchronization work first goes to an in-memory queue and waits for an synchronization slot.
Then the synchronization is marked as pending. Once the slot is released the pending synchronization becomes an active one. 

The information about what files await for the synchronization you will find under `/synchronization/pending` endpoint, while the info about currently running synchronization are exposed under `/synchronization/active`.

##Destination side

After a file synchronization RavenFS stores [a configuration](configurations#syncresult-filename) item that contains details about already performed operation. You can retrieve all of them by using `/synchronization/finished` address.