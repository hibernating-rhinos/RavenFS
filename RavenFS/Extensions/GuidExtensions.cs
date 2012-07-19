using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

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