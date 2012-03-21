using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using RavenFS.Infrastructure;
using RavenFS.Notifications;
using SignalR.Infrastructure;

namespace RavenFS.Controllers
{
    public class NotificationsController : ConnectionController<NotificationEndpoint>
    {
        public NotificationsController()
        {
        }
    }
}