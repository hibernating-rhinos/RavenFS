using System;

namespace RavenFS.Extensions
{
    public static class GuidExtensions
    {
        public static Guid TransfromToGuidWithProperSorting(this byte[] bytes)
        {
            return new Guid(bytes);
        }

        public static byte[] TransformToValueForEsentSorting(this Guid guid)
        {
            return guid.ToByteArray();
        }
    }
}