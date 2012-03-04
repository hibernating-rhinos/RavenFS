using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using RavenFS.Rdc.Wrapper;

namespace RavenFS.Rdc.Wrapper
{
    public static class RdcBufferTools
    {        
        public static int IntPtrCopy(IntPtr source, Stream dest, int length)
        {
            var buffer = new Byte[length];
            Marshal.Copy(source, buffer, 0, length);
            dest.Write(buffer, 0, length);
            return length;
        }

        public static int IntPtrCopy(Stream source, IntPtr dest, int length)
        {
            var buffer = new Byte[length];
            var read = 0;
            var lastRead = 0;
            do
            {
                lastRead = source.Read(buffer, read, length - read);
                read += lastRead;
            } while (lastRead != 0 && read < length);
            Marshal.Copy(buffer, 0, dest, read);
            return read;
        }        
    }
}
