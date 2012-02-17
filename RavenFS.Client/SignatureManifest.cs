using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RavenFS.Client
{
    public class SignatureManifest
    {
        public string FileName { get; set; }
        public IList<Signature> Signatures { get; set; }
        public long FileLength { get; set; }
    }
}
