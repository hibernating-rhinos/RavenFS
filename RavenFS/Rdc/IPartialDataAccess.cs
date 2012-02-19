using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;

namespace RavenFS.Rdc
{
    public interface IPartialDataAccess
    {
        void CopyTo(Stream target, long from, long length);
    }
}