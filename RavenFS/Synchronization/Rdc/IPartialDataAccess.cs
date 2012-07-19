namespace RavenFS.Synchronization.Rdc
{
	using System.IO;
	using System.Threading.Tasks;

	public interface IPartialDataAccess
    {
        Task CopyToAsync(Stream target, long from, long length);
    }
}