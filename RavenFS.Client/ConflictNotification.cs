using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RavenFS.Client
{
    public class ConflictNotification : Notification
    {
        public string FileName { get; set; }
    }
}
