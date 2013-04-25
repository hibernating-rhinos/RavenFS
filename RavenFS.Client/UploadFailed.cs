using System;

namespace RavenFS.Client
{
	public class UploadFailed : Notification
	{
		public Guid UploadId { get; set; }
		public string File { get; set; }
	}
}