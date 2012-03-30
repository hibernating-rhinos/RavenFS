using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace RavenFS.Notifications
{
    public class ConfigChange : Notification
    {
        public string Name { get; set; }

        public ConfigChangeAction Action { get; set; }
    }

    public enum ConfigChangeAction
    {
        Set,
        Delete,
    }
}