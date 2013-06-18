using System;
using System.Collections.Specialized;
using System.IO;
using System.Threading.Tasks;

namespace RavenFS.Client
{
	public interface IRavenFileSystemClient
	{
		Task<ServerStats> StatsAsync();
		Task DeleteAsync(string filename);
		Task RenameAsync(string filename, string rename);
		Task<FileInfo[]> BrowseAsync(int start = 0, int pageSize = 25);
		Task<string[]> GetSearchFieldsAsync(int start = 0, int pageSize = 25);
		Task<SearchResults> SearchAsync(string query, string[] sortFields = null, int start = 0, int pageSize = 25);
		Task<NameValueCollection> GetMetadataForAsync(string filename);
		Task<NameValueCollection> DownloadAsync(string filename, Stream destination, long? from = null, long? to = null);
		Task UpdateMetadataAsync(string filename, NameValueCollection metadata);
		Task UploadAsync(string filename, Stream source);
		Task UploadAsync(string filename, NameValueCollection metadata, Stream source);
		Task UploadAsync(string filename, NameValueCollection metadata, Stream source, Action<string, long> progress);
		Task<string[]> GetFoldersAsync(string from = null, int start = 0,int pageSize = 25);
		Task<SearchResults> GetFilesAsync(string folder, FilesSortOptions options = FilesSortOptions.Default,
		                                                  string fileNameSearchPattern = "", int start = 0, int pageSize = 25);
		Task<Guid> GetServerId();
	}
}