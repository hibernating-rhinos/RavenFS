using System;

namespace RavenFS.Studio.Extensions
{
    public static class DoubleExtensions
    {
        public static double Clamp(this double value, double min, double max)
        {
            return Math.Min(Math.Max(value, min), max);
        }
    }
}
