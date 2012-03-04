using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using RavenFS.Rdc.Wrapper;

namespace RavenFS.Rdc.Wrapper
{
    public class SignatureInfo
    {
        public string Name
        {
            get;
            set;
        }

        public long Length
        {
            get;
            set;
        }

        public SignatureInfo()
        {
            Name = Guid.NewGuid().ToString();
        }

        public SignatureInfo(string name)
        {
            Name = name;
        }        
    }
}
