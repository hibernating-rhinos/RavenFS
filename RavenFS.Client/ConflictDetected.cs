namespace RavenFS.Client
{
	public class ConflictDetected : ConflictNotification
	{
		public string SourceServerUrl { get; set; }
	}
}