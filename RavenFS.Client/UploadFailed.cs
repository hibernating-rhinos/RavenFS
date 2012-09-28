namespace RavenFS.Client
{
	using System;

	public class UploadFailed : Notification
	{
		public Guid UploadId { get; set; }

		public string File { get; set; }
	}
}