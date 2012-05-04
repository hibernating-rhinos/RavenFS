using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace RavenFS.Rdc
{
    public class ConflictItem
    {
        public HistoryItem Remote { get; set; }
        public HistoryItem Current { get; set; }
    }
}