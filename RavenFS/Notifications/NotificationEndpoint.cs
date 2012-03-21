using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using SignalR;

namespace RavenFS.Notifications
{
    public class NotificationEndpoint : PersistentConnection
    {
        public NotificationEndpoint()
        {
        }

        protected override System.Threading.Tasks.Task OnReceivedAsync(string connectionId, string data)
        {
           return base.OnReceivedAsync(connectionId, data);
        }
    }
}