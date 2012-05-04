using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using RavenFS.Client;

namespace RavenFS.Rdc
{
    public class ConflictResolution
    {
        public ConflictResolutionStrategy Strategy { get; set; }
        public string RemoteServerUrl { get; set; }
        public long Version { get; set; }
        public string RemoteServerId { get; set; }
    }
}