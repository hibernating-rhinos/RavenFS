using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace RavenFS.Rdc
{
    public class ConflictItem
    {
        public HistoryItem Theirs { get; set; }
        public HistoryItem Ours { get; set; }
    }
}