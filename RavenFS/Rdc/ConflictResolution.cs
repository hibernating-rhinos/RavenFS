using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace RavenFS.Rdc
{
    public class ConflictResolution
    {
        public ConflictResolutionStrategy Strategy { get; set; }
        public string TheirServerUrl { get; set; }
        public long Version { get; set; }
        public string TheirServerId { get; set; }
    }
}