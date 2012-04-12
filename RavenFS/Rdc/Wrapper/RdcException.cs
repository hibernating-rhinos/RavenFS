using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RavenFS.Rdc.Wrapper;
using RavenFS.Rdc.Wrapper.Unmanaged;

namespace RavenFS.Rdc.Wrapper
{
    public class RdcException : Exception
    {

        public RdcException(string message, Exception innerException) :
            base(message, innerException) { }

        public RdcException(string format, params object[] args) :
            base(String.Format(format, args)) { }

        public RdcException(string message, int hr, RdcError? rdcError = null) :
            base(String.Format("{0} hr: {1} rdcError: {2}", message, hr, rdcError))
        {
        }
    }
        
}
