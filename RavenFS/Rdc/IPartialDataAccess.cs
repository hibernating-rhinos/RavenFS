using System.IO;

namespace RavenFS.Rdc
{
    public interface IPartialDataAccess
    {
        void CopyTo(Stream target, long from, long length);
    }
}