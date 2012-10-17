namespace RavenFS.Notifications
{
	using Client;

	public interface INotificationPublisher
	{
		void Publish(Notification change);
	}
}