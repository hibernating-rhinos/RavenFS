namespace RavenFS.Client
{
	public class SynchronizationUpdate : Notification
	{
		public string FileName { get; set; }

		public string DestinationServer { get; set; }

		public string SourceServer { get; set; }

		public SynchronizationType Type { get; set; }

		public SynchronizationAction Action { get; set; }

		public SynchronizationDirection SynchronizationDirection { get; set; }
	}

	public enum SynchronizationAction
	{
		Start,
		Finish
	}

	public enum SynchronizationDirection
	{
		Outgoing,
		Incoming
	}
}