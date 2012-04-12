namespace RavenFS.Client
{
	public class ConflictDetected : Notification
	{
		public string FileName { get; set; }

		public string ServerUrl { get; set; }
	}
}