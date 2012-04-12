using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace RavenFS.Rdc
{
    public class HistoryItem
    {
        public long Version { get; set; }
        public string ServerId { get; set; }
    }
}