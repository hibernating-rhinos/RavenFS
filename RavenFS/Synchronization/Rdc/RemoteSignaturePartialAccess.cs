namespace RavenFS.Synchronization.Rdc
{
	using System.IO;
	using System.Threading.Tasks;
	using RavenFS.Client;

	public class RemoteSignaturePartialAccess : IPartialDataAccess
    {
        private readonly RavenFileSystemClient _ravenFileSystemClient;
        private readonly string _fileName;

        public RemoteSignaturePartialAccess(RavenFileSystemClient ravenFileSystemClient, string fileName)
        {
            _ravenFileSystemClient = ravenFileSystemClient;
            _fileName = fileName;
        }

        public Task CopyToAsync(Stream target, long from, long length)
        {
			return _ravenFileSystemClient.Synchronization.DownloadSignatureAsync(_fileName, target, from, from + length);
        }
    }
}