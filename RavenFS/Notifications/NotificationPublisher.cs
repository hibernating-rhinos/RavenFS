using RavenFS.Infrastructure.Connections;
using SignalR;

namespace RavenFS.Notifications
{
    public class NotificationPublisher
    {
        private readonly TransportState transportState;
        private readonly IDependencyResolver dependencyResolver;
        private readonly IConnection connection ;

        public NotificationPublisher(TransportState transportState)
        {
            this.transportState = transportState;
            dependencyResolver = new DefaultDependencyResolver();

            var serializer = new TypeHidingJsonSerializer();
            dependencyResolver.Register(typeof(IJsonSerializer), () => serializer);

        	connection =
        		dependencyResolver.Resolve<IConnectionManager>().GetConnectionContext<NotificationEndpoint>().Connection;
        }

        public IDependencyResolver SignalRDependencyResolver
        {
            get { return dependencyResolver; }
        }

        public void Publish(Notification change)
        {
            transportState.Send(change);
            connection.Broadcast(change);
        }
    }
}