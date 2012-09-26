namespace RavenFS.Notifications
{
	public interface INotificationPublisher
	{
		void Publish(Notification change);
	}
}