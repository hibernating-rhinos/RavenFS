using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RavenFS.Tests
{
    public static class TimeMeasure
    {
        public static TimeSpan HowLong(Action action)
        {
            var start = DateTime.Now;
            action();
            return DateTime.Now - start;
        }
    }
}
