using System.IO;
using System.Threading.Tasks;

namespace RavenFS.Rdc
{
    public interface IPartialDataAccess
    {
        Task CopyToAsync(Stream target, long from, long length);
    }
}