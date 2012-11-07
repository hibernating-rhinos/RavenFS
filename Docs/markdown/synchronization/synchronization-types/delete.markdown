#Delete

Every file modification has to be reflected on the destination server. The same is for a delete file operation. When the file is deleted there is created some marker in the system that allows us to detect that
the destination server has the file that was already deleted on the source. The delete synchronization is actually sending a POST message to destination server `/synchronization/delete` endpoint.