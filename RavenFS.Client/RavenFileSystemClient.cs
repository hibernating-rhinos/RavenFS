using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
#if SILVERLIGHT
using System.Net.Browser;
#endif
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Linq;
using RavenFS.Client.Changes;

namespace RavenFS.Client
{
	using System.Collections.Concurrent;
	using System.Threading;

	public class RavenFileSystemClient : IDisposable
	{
		private readonly string baseUrl;
	    private readonly ServerNotifications notifications;
		private IDisposable failedUploadsObservator = null;

		private readonly ConcurrentDictionary<Guid, CancellationTokenSource> uploadCancellationTokens =
			new ConcurrentDictionary<Guid, CancellationTokenSource>();

#if SILVERLIGHT
		static RavenFileSystemClient()
		{
			WebRequest.RegisterPrefix("http://", WebRequestCreator.ClientHttp);
			WebRequest.RegisterPrefix("https://", WebRequestCreator.ClientHttp);
		}
#endif

		public RavenFileSystemClient(string baseUrl)
		{
			this.baseUrl = baseUrl;
			if (ServerUrl.EndsWith("/"))
				this.baseUrl = ServerUrl.Substring(0, ServerUrl.Length - 1);

            notifications = new ServerNotifications(baseUrl);
		}

		public string ServerUrl
		{
			get { return baseUrl; }
		}

		public bool IsObservingFailedUploads
		{
			get { return failedUploadsObservator != null; }
			set
			{
				if (value)
				{
					failedUploadsObservator = notifications.FailedUploads().Subscribe(CancelFileUpload);
				}
				else
				{
					failedUploadsObservator.Dispose();
					failedUploadsObservator = null;
				}
			}
		}

		public async Task<ServerStats> StatsAsync()
		{
			var requestUriString = ServerUrl + "/stats";
			var request = (HttpWebRequest)WebRequest.Create(requestUriString.NoCache());

			try
			{
				var webResponse = await request.GetResponseAsync();
				using (var stream = webResponse.GetResponseStream())
					{
						return new JsonSerializer().Deserialize<ServerStats>(new JsonTextReader(new StreamReader(stream)));
					}
			}
			catch (AggregateException e)
			{
				e.TryThrowBetterError();
				throw;
			}
		}

		public async Task DeleteAsync(string filename)
		{
			var requestUriString = ServerUrl + "/files/" + Uri.EscapeDataString(filename);
			var request = (HttpWebRequest)WebRequest.Create(requestUriString);
			request.Method = "DELETE";

			try
			{
				var webResponse = await request.GetResponseAsync();
				webResponse.Close();
			}
			catch (AggregateException e)
			{
				e.TryThrowBetterError();
				throw;
			}
		}

		public async Task RenameAsync(string filename, string rename)
		{
			var requestUriString = ServerUrl + "/files/" + Uri.EscapeDataString(filename) + "?rename=" + Uri.EscapeDataString(rename);
			var request = (HttpWebRequest)WebRequest.Create(requestUriString);
			request.Method = "PATCH";

			try
			{
				var webResponse = await request.GetResponseAsync();
				webResponse.Close();
			}
			catch (AggregateException e)
			{
				e.TryThrowBetterError();
			}
		}

		public async Task<FileInfo[]> BrowseAsync(int start = 0, int pageSize = 25)
		{
			var request = (HttpWebRequest)WebRequest.Create((ServerUrl + "/files?start=" + start + "&pageSize=" + pageSize).NoCache());

			try
			{
				var webResponse = await request.GetResponseAsync();
				using (var responseStream = webResponse.GetResponseStream())
				using (var streamReader = new StreamReader(responseStream))
				using (var jsonTextReader = new JsonTextReader(streamReader))
				{
					return new JsonSerializer
					{
						Converters =
									{
										new NameValueCollectionJsonConverter()
									}
					}.Deserialize<FileInfo[]>(jsonTextReader);
				}
			}
			catch (AggregateException e)
			{
				e.TryThrowBetterError();
				throw;
			}
		}

        public async Task<string[]> GetSearchFieldsAsync(int start = 0, int pageSize = 25)
        {
            var requestUriString = string.Format("{0}/search/terms?start={1}&pageSize={2}", ServerUrl, start, pageSize).NoCache();
            var request = (HttpWebRequest)WebRequest.Create(requestUriString);

	        try
	        {
		        var webResponse = await request.GetResponseAsync();
				using (var stream = webResponse.GetResponseStream())
				{
					return new JsonSerializer().Deserialize<string[]>(new JsonTextReader(new StreamReader(stream)));
				}
	        }
	        catch (AggregateException e)
	        {
		        e.TryThrowBetterError();
		        throw;
	        }
        }

		public async Task<SearchResults> SearchAsync(string query, string[] sortFields = null, int start = 0, int pageSize = 25)
		{
			var requestUriBuilder = new StringBuilder(ServerUrl)
				.Append("/search/?query=")
				.Append(Uri.EscapeUriString(query))
				.Append("&start=")
				.Append(start)
				.Append("&pageSize=")
				.Append(pageSize);

			if (sortFields != null)
			{
				foreach (var sortField in sortFields)
				{
					requestUriBuilder.Append("&sort=").Append(sortField);
				}
			}
			var request = (HttpWebRequest)WebRequest.Create(requestUriBuilder.ToString().NoCache());
			try
			{
				var webResponse = await request.GetResponseAsync();
				using (var responseStream = webResponse.GetResponseStream())
				using (var streamReader = new StreamReader(responseStream))
				using (var jsonTextReader = new JsonTextReader(streamReader))
				{
					return new JsonSerializer
						{
							Converters =
								{
									new NameValueCollectionJsonConverter()
								}
						}.Deserialize<SearchResults>(jsonTextReader);
				}
			}
			catch (AggregateException e)
			{
				e.TryThrowBetterError();
				throw;
			}
		}

		public async Task<NameValueCollection> GetMetadataForAsync(string filename)
		{
			var request = (HttpWebRequest)WebRequest.Create(ServerUrl + "/files?name=" + Uri.EscapeDataString(filename));
			request.Method = "HEAD";
			try
			{
				var webResponse = await request.GetResponseAsync();

				try
				{
					return new NameValueCollection(webResponse.Headers);
				}
				catch (AggregateException e)
				{
					var we = e.ExtractSingleInnerException() as WebException;
					if (we == null)
						throw;
					var httpWebResponse = we.Response as HttpWebResponse;
					if (httpWebResponse == null)
						throw;
					if (httpWebResponse.StatusCode == HttpStatusCode.NotFound)
						return null;
					throw;
				}
			}
			catch (AggregateException e)
			{
				e.TryThrowBetterError();
				throw;
			}
		}

		public Task<NameValueCollection> DownloadAsync(string filename, Stream destination, long? from = null, long? to = null)
		{
			return DownloadAsync("/files/", filename, destination, from, to);
		}

		private async Task<NameValueCollection> DownloadAsync(string path, string filename, Stream destination,
		                                                      long? from = null, long? to = null,
		                                                      Action<string, long> progress = null)
		{
#if SILVERLIGHT
            if (from != null || to != null)
            {
                throw new NotSupportedException("Silverlight doesn't support partial requests");
            }
#endif

			var collection = new NameValueCollection();
			if (destination.CanWrite == false)
				throw new ArgumentException("Stream does not support writing");

			var request = (HttpWebRequest) WebRequest.Create(ServerUrl + path + filename);

#if !SILVERLIGHT
			if (from != null)
			{
				if (to != null)
					request.AddRange(from.Value, to.Value);
				else
					request.AddRange(from.Value);
			}
			else if (destination.CanSeek)
			{
				destination.Position = destination.Length;
				request.AddRange(destination.Position);
			}
#endif

			try
			{
				var webResponse = await request.GetResponseAsync();
				foreach (var header in webResponse.Headers.AllKeys)
				{
					collection[header] = webResponse.Headers[header];
				}
				var responseStream = webResponse.GetResponseStream();
				await responseStream.CopyToAsync(destination, i =>
					{
						if (progress != null)
							progress(filename, i);
					});
				return collection;
			}
			catch (AggregateException e)
			{
				e.TryThrowBetterError();
				throw;
			}
		}

		public async Task UpdateMetadataAsync(string filename, NameValueCollection metadata)
		{
			var request = (HttpWebRequest)WebRequest.Create(ServerUrl + "/files/" + filename);

			request.Method = "POST";
			request.ContentLength = 0;
			AddHeaders(metadata, request);

			try
			{
				await request.GetResponseAsync();
			}
			catch (AggregateException e)
			{
				e.TryThrowBetterError();
			}
		}

		public Task UploadAsync(string filename, Stream source)
		{
			return UploadAsync(filename, new NameValueCollection(), source, null);
		}

		public Task UploadAsync(string filename, NameValueCollection metadata, Stream source)
		{
			return UploadAsync(filename, metadata, source, null);
		}

		public async Task UploadAsync(string filename, NameValueCollection metadata, Stream source,
		                              Action<string, long> progress)
		{
			if (source.CanRead == false)
				throw new AggregateException("Stream does not support reading");

			var uploadIdentifier = Guid.NewGuid();
			var request =
				(HttpWebRequest)
				WebRequest.Create(ServerUrl + "/files?name=" + Uri.EscapeDataString(filename) + "&uploadId=" + uploadIdentifier);
			request.Method = "PUT";
			request.AllowWriteStreamBuffering = false;

#if !SILVERLIGHT
			request.SendChunked = true;
#else
			if (source.CanSeek)
			{
				request.ContentLength = source.Length;
			}
#endif
			AddHeaders(metadata, request);

			var cts = new CancellationTokenSource();

			RegisterUploadOperation(uploadIdentifier, cts);

			try
			{
				var destination = await request.GetRequestStreamAsync();
				await source.CopyToAsync(destination, written =>
				{
					if (progress != null)
						progress(filename, written);
				}, cts.Token);

				UnregisterUploadOperation(uploadIdentifier);
				destination.Close();

				var webResponse = await request.GetResponseAsync();
				webResponse.Close();
			}
			catch (AggregateException e)
			{
				e.TryThrowBetterError();
			}
			
		}

		private void CancelFileUpload(UploadFailed uploadFailed)
		{
			CancellationTokenSource cts;
			if(uploadCancellationTokens.TryGetValue(uploadFailed.UploadId, out cts))
			{
				cts.Cancel();
			}
		}

		private void RegisterUploadOperation(Guid uploadId, CancellationTokenSource cts)
		{
			if (IsObservingFailedUploads)
				uploadCancellationTokens.TryAdd(uploadId, cts);
		}

		private void UnregisterUploadOperation(Guid uploadId)
		{
			if (IsObservingFailedUploads)
			{
				CancellationTokenSource cts;
				uploadCancellationTokens.TryRemove(uploadId, out cts);
			}
		}

		public SynchronizationClient Synchronization
		{
			get
			{
				return new SynchronizationClient(this);
			}
		}

		public ConfigurationClient Config
		{
			get { return new ConfigurationClient(this);}
		}

		public StorageClient Storage
		{
			get
			{
				return new StorageClient(this);
			}
		}

        public IServerNotifications Notifications
        {
            get { return notifications; }
        }

		private static void AddHeaders(NameValueCollection metadata, HttpWebRequest request)
		{
			foreach (var key in metadata.AllKeys)
			{
				var values = metadata.GetValues(key);
				if (values == null)
					continue;
				foreach (var value in values)
				{
					request.Headers[key] = value;
				}
			}
		}

	    public async Task<string[]> GetFoldersAsync(string from = null, int start = 0,int pageSize = 25)
		{
			var path = @from ?? "";
			if (path.StartsWith("/"))
				path = path.Substring(1);

			var requestUriString = ServerUrl + "/folders/subdirectories/" + Uri.EscapeUriString(path) + "?pageSize=" +
			                          pageSize + "&start=" + start;
			var request = (HttpWebRequest)WebRequest.Create(requestUriString.NoCache());

		    try
		    {
			    var webResponse = await request.GetResponseAsync();

			    using (var stream = webResponse.GetResponseStream())
			    {
				    return new JsonSerializer().Deserialize<string[]>(new JsonTextReader(new StreamReader(stream)));
			    }
		    }
		    catch (AggregateException e)
		    {
				e.TryThrowBetterError();
			    throw;
		    }
		}

		public Task<SearchResults> GetFilesAsync(string folder, FilesSortOptions options = FilesSortOptions.Default, string fileNameSearchPattern = "", int start = 0, int pageSize = 25)
		{
		    var folderQueryPart = GetFolderQueryPart(folder);

            if (string.IsNullOrEmpty(fileNameSearchPattern) == false && fileNameSearchPattern.Contains("*") == false && fileNameSearchPattern.Contains("?") == false)
			{
                fileNameSearchPattern = fileNameSearchPattern + "*";
			}
		    var fileNameQueryPart = GetFileNameQueryPart(fileNameSearchPattern);

		    return SearchAsync(folderQueryPart + fileNameQueryPart, GetSortFields(options), start, pageSize);
		}

		public async Task<Guid> GetServerId()
		{
			var requestUriString = ServerUrl + "/id";
			var request = (HttpWebRequest)WebRequest.Create(requestUriString);

			try
			{
				var webResponse = await request.GetResponseAsync();

				using (var stream = webResponse.GetResponseStream())
				{
					return new JsonSerializer().Deserialize<Guid>(new JsonTextReader(new StreamReader(stream)));
				}
			}
			catch (AggregateException e)
			{
				e.TryThrowBetterError();
				throw;
			}
		}

	    private static string GetFileNameQueryPart(string fileNameSearchPattern)
	    {
		    if (string.IsNullOrEmpty(fileNameSearchPattern))
			    return "";

		    if (fileNameSearchPattern.StartsWith("*") || (fileNameSearchPattern.StartsWith("?")))
			    return " AND __rfileName:" + Reverse(fileNameSearchPattern);

		    return " AND __fileName:" + fileNameSearchPattern;
	    }

		private static string Reverse(string value)
        {
            var characters = value.ToCharArray();
            Array.Reverse(characters);

            return new string(characters);
        }

	    private static string GetFolderQueryPart(string folder)
	    {
	        if (folder == null) throw new ArgumentNullException("folder");
	        if (folder.StartsWith("/") == false)
	            throw new ArgumentException("folder must starts with a /", "folder");

	        int level;
	        if (folder == "/")
	            level = 1;
	        else
	            level = folder.Count(ch => ch == '/') + 1;

	        var folderQueryPart = "__directory:" + folder + " AND __level:" + level;
	        return folderQueryPart;
	    }

	    private static string[] GetSortFields(FilesSortOptions options)
		{
			string sort = null;
			switch (options & ~FilesSortOptions.Desc)
			{
				case FilesSortOptions.Name:
					sort = "__key";
					break;
				case FilesSortOptions.Size:
					sort = "__size";
					break;
				case FilesSortOptions.LastModified:
					sort = "__modified";
					break;
			}

			if (options.HasFlag(FilesSortOptions.Desc))
			{
				if (string.IsNullOrEmpty(sort))
					throw new ArgumentException("options");
				sort = "-" + sort;
			}

			var sortFields = string.IsNullOrEmpty(sort) ? null : new[] {sort};
			return sortFields;
		}

		public class ConfigurationClient
		{
			private readonly RavenFileSystemClient ravenFileSystemClient;
			private readonly JsonSerializer jsonSerializer;

			public ConfigurationClient(RavenFileSystemClient ravenFileSystemClient)
			{
				jsonSerializer = new JsonSerializer
				{
					Converters =
						{
							new NameValueCollectionJsonConverter()
						}
				};

				this.ravenFileSystemClient = ravenFileSystemClient;
			}

			public async Task<string[]> GetConfigNames(int start = 0, int pageSize = 25)
			{
				var requestUriString = ravenFileSystemClient.ServerUrl + "/config?start=" + start + "&pageSize=" + pageSize;
				var request = (HttpWebRequest) WebRequest.Create(requestUriString.NoCache());

				try
				{
					var webResponse = await request.GetResponseAsync();
					using (var responseStream = webResponse.GetResponseStream())
					{
						return jsonSerializer.Deserialize<string[]>(new JsonTextReader(new StreamReader(responseStream)));
					}
				}
				catch (AggregateException e)
				{
					e.TryThrowBetterError();
					throw;
				}
			}

			public async Task SetConfig(string name, NameValueCollection data)
			{
				var requestUriString = ravenFileSystemClient.ServerUrl + "/config?name=" + StringUtils.UrlEncode(name);
				var request = (HttpWebRequest) WebRequest.Create(requestUriString);
				request.Method = "PUT";

				var stream = await request.GetRequestStreamAsync();

				using (var streamWriter = new StreamWriter(stream))
				{
					jsonSerializer.Serialize(streamWriter, data);
					streamWriter.Flush();
				}

				await request.GetResponseAsync();
			}

			public Task DeleteConfig(string name)
			{
				var requestUriString = ravenFileSystemClient.ServerUrl + "/config?name=" + StringUtils.UrlEncode(name);
				var request = (HttpWebRequest)WebRequest.Create(requestUriString);
				request.Method = "DELETE";
				return request.GetResponseAsync();
			}

			public async Task<NameValueCollection> GetConfig(string name)
			{
				var requestUriString = ravenFileSystemClient.ServerUrl + "/config?name=" + StringUtils.UrlEncode(name);
				var request = (HttpWebRequest) WebRequest.Create(requestUriString.NoCache());

				var response = await request.GetResponseAsync();

				try
				{
					var webResponse = response;
					return jsonSerializer.Deserialize<NameValueCollection>(
							new JsonTextReader(new StreamReader(webResponse.GetResponseStream())));
				}
				catch (AggregateException e)
				{
					var webException = e.ExtractSingleInnerException() as WebException;
					if (webException == null)
						throw;
					var httpWebResponse = webException.Response as HttpWebResponse;
					if (httpWebResponse == null)
						throw;
					if (httpWebResponse.StatusCode == HttpStatusCode.NotFound)
						return null;
					throw;
				}
			}

			public async Task<ConfigSearchResults> SearchAsync(string prefix, int start = 0, int pageSize = 25)
			{
				var requestUriBuilder = new StringBuilder(ravenFileSystemClient.ServerUrl)
					.Append("/config/search/?prefix=")
					.Append(Uri.EscapeUriString(prefix))
					.Append("&start=")
					.Append(start)
					.Append("&pageSize=")
					.Append(pageSize);

				var request = (HttpWebRequest) WebRequest.Create(requestUriBuilder.ToString().NoCache());

				try
				{
					var webResponse = await request.GetResponseAsync();
					using (var responseStream = webResponse.GetResponseStream())
					using (var streamReader = new StreamReader(responseStream))
					using (var jsonTextReader = new JsonTextReader(streamReader))
					{
						return new JsonSerializer().Deserialize<ConfigSearchResults>(jsonTextReader);
					}
				}
				catch (AggregateException e)
				{
					e.TryThrowBetterError();
					throw;
				}
			}
		}

		public class SynchronizationClient
		{
			private readonly RavenFileSystemClient ravenFileSystemClient;

			public SynchronizationClient(RavenFileSystemClient ravenFileSystemClient)
			{
				this.ravenFileSystemClient = ravenFileSystemClient;
			}

			internal Task DownloadSignatureAsync(string sigName, Stream destination, long? from = null, long? to = null)
			{
				return ravenFileSystemClient.DownloadAsync("/rdc/signatures/", sigName, destination, from, to);
			}

			internal async Task<SignatureManifest> GetRdcManifestAsync(string path)
			{
				var requestUriString = ravenFileSystemClient.ServerUrl + "/rdc/manifest/" + StringUtils.UrlEncode(path);
				var request = (HttpWebRequest)WebRequest.Create(requestUriString);

				try
				{
					var webResponse = await request.GetResponseAsync();

					using (var stream = webResponse.GetResponseStream())
					{
						return new JsonSerializer().Deserialize<SignatureManifest>(new JsonTextReader(new StreamReader(stream)));
					}
				}
				catch (AggregateException e)
				{
					e.TryThrowBetterError();
					throw;
				}
			}

			public async Task<DestinationSyncResult[]> SynchronizeDestinationsAsync(bool forceSyncingAll = false)
			{
				var requestUriString = String.Format("{0}/synchronization/ToDestinations?forceSyncingAll={1}", ravenFileSystemClient.ServerUrl, forceSyncingAll);
				var request = (HttpWebRequest)WebRequest.Create(requestUriString);
				request.Method = "POST";
				request.ContentLength = 0;
				try
				{
					var webResponse = await request.GetResponseAsync();

					using (var stream = webResponse.GetResponseStream())
					{
						return new JsonSerializer().Deserialize<DestinationSyncResult[]>(new JsonTextReader(new StreamReader(stream)));
					}
				}
				catch (AggregateException e)
				{
					e.TryThrowBetterError();
					throw;
				}

			}

			public async Task<SynchronizationReport> StartAsync(string fileName, string destinationServerUrl)
			{
				var requestUriString = String.Format("{0}/synchronization/start/{1}?destinationServerUrl={2}", ravenFileSystemClient.ServerUrl, Uri.EscapeDataString(fileName), Uri.EscapeDataString(destinationServerUrl));
				var request = (HttpWebRequest)WebRequest.Create(requestUriString);
				request.Method = "POST";
				request.ContentLength = 0;
				try
				{
					var webResponse = await request.GetResponseAsync();

					using (var stream = webResponse.GetResponseStream())
					{
						return new JsonSerializer().Deserialize<SynchronizationReport>(new JsonTextReader(new StreamReader(stream)));
					}
				}
				catch (AggregateException e)
				{
					e.TryThrowBetterError();
					throw;
				}
			}

			public async Task<SynchronizationReport> GetSynchronizationStatusAsync(string fileName)
			{
				var requestUriString = String.Format("{0}/synchronization/status/{1}", ravenFileSystemClient.ServerUrl,
				                                     Uri.EscapeDataString(fileName));
				var request = (HttpWebRequest) WebRequest.Create(requestUriString.NoCache());
				request.ContentLength = 0;

				try
				{
					var webResponse = await request.GetResponseAsync();
					using (var stream = webResponse.GetResponseStream())
					{
						return new JsonSerializer().Deserialize<SynchronizationReport>(new JsonTextReader(new StreamReader(stream)));
					}

				}
				catch (AggregateException e)
				{
					e.TryThrowBetterError();
					throw;
				}
			}

			public async Task ResolveConflictAsync(string filename, ConflictResolutionStrategy strategy)
            {
                var requestUriString = String.Format("{0}/synchronization/resolveConflict/{1}?strategy={2}",
                    ravenFileSystemClient.ServerUrl, Uri.EscapeDataString(filename), Uri.EscapeDataString(strategy.ToString()));
                var request = (HttpWebRequest)WebRequest.Create(requestUriString);
                request.Method = "PATCH";

				try
				{
					var webResponse = await request.GetResponseAsync();
					webResponse.Close();
				}
				catch (AggregateException e)
				{
					e.TryThrowBetterError();
				}
            }

			internal async Task ApplyConflictAsync(string filename, long remoteVersion, string remoteServerId,
			                                       IList<HistoryItem> remoteHistory, string remoteServerUrl)
			{
				var requestUriString =
					String.Format("{0}/synchronization/applyConflict/{1}?remoteVersion={2}&remoteServerId={3}&remoteServerUrl={4}",
					              ravenFileSystemClient.ServerUrl, Uri.EscapeDataString(filename), remoteVersion,
					              Uri.EscapeDataString(remoteServerId), Uri.EscapeDataString(remoteServerUrl));
				var request = (HttpWebRequest) WebRequest.Create(requestUriString);
				request.Method = "PATCH";

				try
				{
					var stream = await request.GetRequestStreamAsync();

					var sb = new StringBuilder();
					var jw = new JsonTextWriter(new StringWriter(sb));
					new JsonSerializer().Serialize(jw, remoteHistory);
					var bytes = Encoding.UTF8.GetBytes(sb.ToString());

					await stream.WriteAsync(bytes, 0, bytes.Length);
					stream.Close();
					await request.GetResponseAsync();
				}
				catch (AggregateException e)
				{
					e.TryThrowBetterError();
					throw;
				}
			}

			public async Task<ListPage<SynchronizationReport>> GetFinishedAsync(int page = 0, int pageSize = 25)
            {
                var requestUriString = String.Format("{0}/synchronization/finished?start={1}&pageSize={2}", ravenFileSystemClient.ServerUrl, page, pageSize);
                var request = (HttpWebRequest)WebRequest.Create(requestUriString.NoCache());
                request.ContentLength = 0;
				try
				{
					var webResponse = await request.GetResponseAsync();

					using (var stream = webResponse.GetResponseStream())
					{
						var preResult =
							new JsonSerializer().Deserialize<ListPage<SynchronizationReport>>(new JsonTextReader(new StreamReader(stream)));
						return preResult;
					}
				}
				catch (AggregateException e)
				{
					e.TryThrowBetterError();
					throw;
				}

            }

			public async Task<ListPage<SynchronizationDetails>> GetActiveAsync(int page = 0, int pageSize = 25)
			{
				var requestUriString = String.Format("{0}/synchronization/active?start={1}&pageSize={2}",
				                                     ravenFileSystemClient.ServerUrl, page, pageSize);
				var request = (HttpWebRequest) WebRequest.Create(requestUriString.NoCache());
				request.ContentLength = 0;
				try
				{
					var webResponse = await request.GetResponseAsync();

					using (var stream = webResponse.GetResponseStream())
					{
						var preResult =
							new JsonSerializer().Deserialize<ListPage<SynchronizationDetails>>(new JsonTextReader(new StreamReader(stream)));
						return preResult;
					}
				}
				catch (AggregateException e)
				{
					e.TryThrowBetterError();
					throw;
				}
			}

			public async Task<ListPage<SynchronizationDetails>> GetPendingAsync(int page = 0, int pageSize = 25)
			{
				var requestUriString = String.Format("{0}/synchronization/pending?start={1}&pageSize={2}",
				                                     ravenFileSystemClient.ServerUrl, page, pageSize);
				var request = (HttpWebRequest) WebRequest.Create(requestUriString.NoCache());
				request.ContentLength = 0;

				try
				{
					var webResponse = await request.GetResponseAsync();

					using (var stream = webResponse.GetResponseStream())
					{
						var preResult =
							new JsonSerializer().Deserialize<ListPage<SynchronizationDetails>>(new JsonTextReader(new StreamReader(stream)));
						return preResult;
					}
				}
				catch (AggregateException e)
				{
					e.TryThrowBetterError();
					throw;
				}
			}

			internal async Task<SourceSynchronizationInformation> GetLastSynchronizationFromAsync(Guid serverId)
			{
				var requestUriString = String.Format("{0}/synchronization/LastSynchronization?from={1}",
				                                     ravenFileSystemClient.ServerUrl, serverId);
				var request = (HttpWebRequest) WebRequest.Create(requestUriString.NoCache());
				request.ContentLength = 0;
				try
				{
					var webResponse = await request.GetResponseAsync();

					using (var stream = webResponse.GetResponseStream())
					{
						var preResult =
							new JsonSerializer().Deserialize<SourceSynchronizationInformation>(new JsonTextReader(new StreamReader(stream)));
						return preResult;
					}
				}
				catch (AggregateException e)
				{
					e.TryThrowBetterError();
					throw;
				}
			}

			internal async Task<IEnumerable<SynchronizationConfirmation>> ConfirmFilesAsync(IEnumerable<Tuple<string, Guid>> sentFiles)
			{
				var requestUriString = String.Format("{0}/synchronization/Confirm", ravenFileSystemClient.ServerUrl);
				var request = (HttpWebRequest)WebRequest.Create(requestUriString);
				request.ContentType = "application/json";
				request.Method = "POST";
				try
				{
					var stream = await request.GetRequestStreamAsync();

					var sb = new StringBuilder();
					var jw = new JsonTextWriter(new StringWriter(sb));
					new JsonSerializer().Serialize(jw, sentFiles);
					var bytes = Encoding.UTF8.GetBytes(sb.ToString());

					await stream.WriteAsync(bytes, 0, bytes.Length);
					stream.Close();
					var webResponse = await request.GetResponseAsync();

					using (var responseStream = webResponse.GetResponseStream())
					{
						return
							new JsonSerializer().Deserialize<IEnumerable<SynchronizationConfirmation>>(
								new JsonTextReader(new StreamReader(responseStream)));
					}
				}
				catch (AggregateException e)
				{
					e.TryThrowBetterError();
					throw;
				}
			}

			public async Task<ListPage<ConflictItem>> GetConflictsAsync(int page = 0, int pageSize = 25)
			{
				var requestUriString = String.Format("{0}/synchronization/conflicts?start={1}&pageSize={2}",
				                                     ravenFileSystemClient.ServerUrl, page, pageSize);
				var request = (HttpWebRequest) WebRequest.Create(requestUriString.NoCache());
				request.ContentLength = 0;
				try
				{
					var webResponse = await request.GetResponseAsync();

					using (var stream = webResponse.GetResponseStream())
					{
						var preResult =
							new JsonSerializer().Deserialize<ListPage<ConflictItem>>(new JsonTextReader(new StreamReader(stream)));
						return preResult;
					}
				}
				catch (AggregateException e)
				{
					e.TryThrowBetterError();
					throw;
				}
			}

			internal async Task IncrementLastETagAsync(Guid sourceServerId, string sourceServerUrl, Guid sourceFileETag)
			{
				var requestUriString =
					String.Format("{0}/synchronization/IncrementLastETag?sourceServerId={1}&sourceServerUrl={2}&sourceFileETag={3}",
					              ravenFileSystemClient.ServerUrl, sourceServerId, sourceServerUrl, sourceFileETag);
				var request = (HttpWebRequest) WebRequest.Create(requestUriString.NoCache());
				request.ContentLength = 0;
				request.Method = "POST";
				try
				{
					var webResponse = await request.GetResponseAsync();
					webResponse.Close();
				}
				catch (AggregateException e)
				{
					e.TryThrowBetterError();
					throw;
				}
			}

			public async Task<RdcStats> GetRdcStatsAsync()
			{
				var requestUriString = ravenFileSystemClient.ServerUrl + "/rdc/stats";
				var request = (HttpWebRequest)WebRequest.Create(requestUriString.NoCache());

				try
				{
					var webResponse = await request.GetResponseAsync();
					using (var stream = webResponse.GetResponseStream())
					{
						return new JsonSerializer().Deserialize<RdcStats>(new JsonTextReader(new StreamReader(stream)));
					}
				}
				catch (AggregateException e)
				{
					e.TryThrowBetterError();
					throw;
				}
			}
		}

		public class StorageClient
		{
			private readonly RavenFileSystemClient ravenFileSystemClient;

			public StorageClient(RavenFileSystemClient ravenFileSystemClient)
			{
				this.ravenFileSystemClient = ravenFileSystemClient;
			}

			public async Task CleanUp()
			{
				var requestUriString = String.Format("{0}/storage/cleanup", ravenFileSystemClient.ServerUrl);
				var request = (HttpWebRequest)WebRequest.Create(requestUriString.NoCache());
				request.ContentLength = 0;
				request.Method = "POST";

				try
				{
					var webResponse = await request.GetResponseAsync();
					webResponse.Close();
				}
				catch (AggregateException e)
				{
					e.TryThrowBetterError();
					throw;
				}
			
			}

			public async Task RetryRenaming()
			{
				var requestUriString = String.Format("{0}/storage/retryrenaming", ravenFileSystemClient.ServerUrl);
				var request = (HttpWebRequest)WebRequest.Create(requestUriString.NoCache());
				request.ContentLength = 0;
				request.Method = "POST";

				try
				{
					var webResponse = await request.GetResponseAsync();
					webResponse.Close();
				}
				catch (AggregateException e)
				{
					e.TryThrowBetterError();
					throw;
				}
				;
			}
		}

	    public void Dispose()
	    {
	        notifications.Dispose();
	    }
	}
}