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
	public class RavenFileSystemClient : IDisposable
	{
		private readonly string baseUrl;
	    private ServerNotifications notifications;

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
			if (this.ServerUrl.EndsWith("/"))
				this.baseUrl = this.ServerUrl.Substring(0, this.ServerUrl.Length - 1);

            notifications = new ServerNotifications(baseUrl);
		}

		public string ServerUrl
		{
			get { return baseUrl; }
		}

		public Task<ServerStats> StatsAsync()
		{
			var requestUriString = ServerUrl + "/stats";
			var request = (HttpWebRequest)WebRequest.Create(requestUriString.NoCache());
			return request.GetResponseAsync()
				.ContinueWith(task =>
				{
					using (var stream = task.Result.GetResponseStream())
					{
						return new JsonSerializer().Deserialize<ServerStats>(new JsonTextReader(new StreamReader(stream)));
					}
				})
				.TryThrowBetterError();
		}

		public Task DeleteAsync(string filename)
		{
			var requestUriString = ServerUrl + "/files/" + Uri.EscapeDataString(filename);
			var request = (HttpWebRequest)WebRequest.Create(requestUriString);
			request.Method = "DELETE";
			return request.GetResponseAsync()
				.ContinueWith(task => task.Result.Close())
				.TryThrowBetterError();
		}

		public Task RenameAsync(string filename, string rename)
		{
			var requestUriString = ServerUrl + "/files/" + Uri.EscapeDataString(filename) + "?rename=" + Uri.EscapeDataString(rename);
			var request = (HttpWebRequest)WebRequest.Create(requestUriString);
			request.Method = "PATCH";
			return request.GetResponseAsync()
				.ContinueWith(task => task.Result.Close())
				.TryThrowBetterError();
		}

		public Task<FileInfo[]> BrowseAsync(int start = 0, int pageSize = 25)
		{
			var request = (HttpWebRequest)WebRequest.Create((ServerUrl + "/files?start=" + start + "&pageSize=" + pageSize).NoCache());
			return request.GetResponseAsync()
				.ContinueWith(task =>
				{
					using (var responseStream = task.Result.GetResponseStream())
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
				})
				.TryThrowBetterError();
		}

        public Task<string[]> GetSearchFieldsAsync(int start = 0, int pageSize = 25)
        {
            var requestUriString = string.Format("{0}/search/terms?start={1}&pageSize={2}", ServerUrl, start, pageSize).NoCache();
            var request = (HttpWebRequest)WebRequest.Create(requestUriString);
            return request.GetResponseAsync()
                .ContinueWith(task =>
                {
                    using (var stream = task.Result.GetResponseStream())
                    {
                        return new JsonSerializer().Deserialize<string[]>(new JsonTextReader(new StreamReader(stream)));
                    }
                })
				.TryThrowBetterError();
        }

		public Task<SearchResults> SearchAsync(string query, string[] sortFields = null, int start = 0, int pageSize = 25)
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
			return request.GetResponseAsync()
				.ContinueWith(task =>
				{
					using (var responseStream = task.Result.GetResponseStream())
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
				})
				.TryThrowBetterError();
		}

		public Task<NameValueCollection> GetMetadataForAsync(string filename)
		{
			var request = (HttpWebRequest)WebRequest.Create(ServerUrl + "/files/" + filename);
			request.Method = "HEAD";
			return request.GetResponseAsync()
				.ContinueWith(task =>
				              	{
				              		try
				              		{
				              			var response = task.Result;
				              			return new NameValueCollection(response.Headers);
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
				).TryThrowBetterError();
		}

		public Task<NameValueCollection> DownloadAsync(string filename, Stream destination, long? from = null, long? to = null)
		{
			return DownloadAsync("/files/", filename, destination, from, to);
		}

		private Task<NameValueCollection> DownloadAsync(string path, string filename, Stream destination, long? from = null, long? to = null,
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

			var request = (HttpWebRequest)WebRequest.Create(ServerUrl + path + filename);

#if !SILVERLIGHT
			if (from != null)
			{
				if (to != null)
				{
					request.AddRange(from.Value, to.Value);
				}
				else
				{
					request.AddRange(from.Value);
				}
			}
			else if (destination.CanSeek)
			{
				destination.Position = destination.Length;
				request.AddRange(destination.Position);
			}
#endif
			progress = progress ?? delegate { };
			return request.GetResponseAsync()
				.ContinueWith(task =>
				{
					foreach (var header in task.Result.Headers.AllKeys)
					{
						collection[header] = task.Result.Headers[header];
					}
					var responseStream = task.Result.GetResponseStream();
					return responseStream.CopyToAsync(destination, i => progress(filename, i))
						.ContinueWith(_ =>
						{
							task.Result.Close();
							return collection;
						});
				})
				.Unwrap()
				.TryThrowBetterError();
		}

		public Task UpdateMetadataAsync(string filename, NameValueCollection metadata)
		{
			var request = (HttpWebRequest)WebRequest.Create(ServerUrl + "/files/" + filename);

			request.Method = "POST";
			request.ContentLength = 0;
			AddHeaders(metadata, request);
			return request
				.GetResponseAsync()
				.TryThrowBetterError();
		}

		public Task UploadAsync(string filename, Stream source)
		{
			return UploadAsync(filename, new NameValueCollection(), source, null);
		}

		public Task UploadAsync(string filename, NameValueCollection metadata, Stream source)
		{
			return UploadAsync(filename, metadata, source, null);
		}

		public Task UploadAsync(string filename, NameValueCollection metadata, Stream source, Action<string, long> progress)
		{
			if (source.CanRead == false)
				throw new AggregateException("Stream does not support reading");

			var request = (HttpWebRequest)WebRequest.Create(ServerUrl + "/files/" + filename);
			request.Method = "PUT";
#if !SILVERLIGHT
			request.SendChunked = true;
			request.AllowWriteStreamBuffering = false;
#else
			if (source.CanSeek)
			{
				request.ContentLength = source.Length;
				request.AllowWriteStreamBuffering = false;
			}
#endif
			request.ContentLength = source.Length;
			request.AllowWriteStreamBuffering = false;

			AddHeaders(metadata, request);
			return request.GetRequestStreamAsync()
				.ContinueWith(
					task => source.CopyToAsync(
						task.Result,
						written =>
						{
							if (progress != null)
								progress(filename, written);
						})
				.ContinueWith(_ => task.Result.Close())
				)
				.Unwrap()
				.ContinueWith(task =>
				{
					task.Wait();
					return request.GetResponseAsync();
				})
				.Unwrap()
				.ContinueWith(task => task.Result.Close())
				.TryThrowBetterError();
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

        public IServerNotifications Notifications
        {
            get { return notifications; }
        }

	    public Task DownloadSignatureAsync(string sigName, Stream destination, long? from = null, long? to = null)
		{
			return DownloadAsync("/rdc/signatures/", sigName, destination, from, to);
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

	    public Task<string[]> GetFoldersAsync(string from = null, int start = 0,int pageSize = 25)
		{
			var path = @from ?? "";
			if (path.StartsWith("/"))
				path = path.Substring(1);

			string requestUriString = ServerUrl + "/folders/subdirectories/" + Uri.EscapeUriString(path) + "?pageSize=" +
			                          pageSize + "&start=" + start;
			var request = (HttpWebRequest)WebRequest.Create(requestUriString.NoCache());
			return request.GetResponseAsync()
				.ContinueWith(task =>
				{
					using (var stream = task.Result.GetResponseStream())
					{
						return new JsonSerializer().Deserialize<string[]>(new JsonTextReader(new StreamReader(stream)));
					}
				})
				.TryThrowBetterError();
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

		public Task<Guid> GetServerId()
		{
			var requestUriString = ServerUrl + "/id";
			var request = (HttpWebRequest)WebRequest.Create(requestUriString);
			return request.GetResponseAsync()
				.ContinueWith(task =>
				{
					using (var stream = task.Result.GetResponseStream())
					{
						return new JsonSerializer().Deserialize<Guid>(new JsonTextReader(new StreamReader(stream)));
					}
				})
				.TryThrowBetterError();
		}

	    private static string GetFileNameQueryPart(string fileNameSearchPattern)
	    {
	    	if (string.IsNullOrEmpty(fileNameSearchPattern))
	        {
	            return "";
	        }
	    	if (fileNameSearchPattern.StartsWith("*") || (fileNameSearchPattern.StartsWith("?")))
	    	{
	    		return " AND __rfileName:" + Reverse(fileNameSearchPattern);
	    	}
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

	        string folderQueryPart = "__directory:" + folder + " AND __level:" + level;
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

			public Task<string[]> GetConfigNames(int start = 0, int pageSize= 25)
			{
				var requestUriString = ravenFileSystemClient.ServerUrl + "/config?start=" + start + "&pageSize=" + pageSize;
				var request = (HttpWebRequest) WebRequest.Create(requestUriString.NoCache());
				return request.GetResponseAsync()
					.ContinueWith(task =>
					{
						using(var responseStream = task.Result.GetResponseStream())
						{
							return jsonSerializer.Deserialize<string[]>(new JsonTextReader(new StreamReader(responseStream)));
						}
					})
					.TryThrowBetterError();
			}

			public Task SetConfig(string name, NameValueCollection data)
			{
				var requestUriString = ravenFileSystemClient.ServerUrl + "/config?name=" + StringUtils.UrlEncode(name);
				var request = (HttpWebRequest)WebRequest.Create(requestUriString);
				request.Method = "PUT";
				return request.GetRequestStreamAsync()
					.ContinueWith(task =>
					{
						using(var streamWriter = new StreamWriter(task.Result))
						{
							jsonSerializer.Serialize(streamWriter, data);
							streamWriter.Flush();
						}
					})
					.ContinueWith(task => request.GetResponseAsync())
					.Unwrap();
			}

			public Task DeleteConfig(string name)
			{
				var requestUriString = ravenFileSystemClient.ServerUrl + "/config?name=" + StringUtils.UrlEncode(name);
				var request = (HttpWebRequest)WebRequest.Create(requestUriString);
				request.Method = "DELETE";
				return request.GetResponseAsync();
			}

			public Task<NameValueCollection> GetConfig(string name)
			{
				var requestUriString = ravenFileSystemClient.ServerUrl + "/config?name=" + StringUtils.UrlEncode(name);
				var request = (HttpWebRequest)WebRequest.Create(requestUriString.NoCache());
				
				return request.GetResponseAsync()
					.ContinueWith(task =>
					{
						WebResponse webResponse;
						try
						{
							webResponse = task.Result;
							return jsonSerializer.Deserialize<NameValueCollection>(new JsonTextReader(new StreamReader(webResponse.GetResponseStream())));
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
					});
			}
		}

		public class SynchronizationClient
		{
			private readonly RavenFileSystemClient ravenFileSystemClient;

			public SynchronizationClient(RavenFileSystemClient ravenFileSystemClient)
			{
				this.ravenFileSystemClient = ravenFileSystemClient;
			}

			public Task<SignatureManifest> GetRdcManifestAsync(string path)
			{
				var requestUriString = ravenFileSystemClient.ServerUrl + "/rdc/manifest/" + StringUtils.UrlEncode(path);
				var request = (HttpWebRequest)WebRequest.Create(requestUriString);
				return request.GetResponseAsync()
					.ContinueWith(task =>
					{
						using (var stream = task.Result.GetResponseStream())
						{
							return new JsonSerializer().Deserialize<SignatureManifest>(new JsonTextReader(new StreamReader(stream)));
						}
					})
					.TryThrowBetterError();
			}

			public Task<IEnumerable<DestinationSyncResult>> SynchronizeDestinationsAsync(bool forceSyncingAll = false)
			{
				var requestUriString = String.Format("{0}/synchronization/ToDestinations?forceSyncingAll={1}", ravenFileSystemClient.ServerUrl, forceSyncingAll);
				var request = (HttpWebRequest)WebRequest.Create(requestUriString);
				request.Method = "POST";
				request.ContentLength = 0;
				return request.GetResponseAsync()
					.ContinueWith(task =>
					{
						using (var stream = task.Result.GetResponseStream())
						{
							return new JsonSerializer().Deserialize<IEnumerable<DestinationSyncResult>>(new JsonTextReader(new StreamReader(stream)));
						}
					})
					.TryThrowBetterError();
			}

			public Task<SynchronizationReport> StartSynchronizationToAsync(string fileName, string destinationServerUrl)
			{
				var requestUriString = String.Format("{0}/synchronization/start/{1}?destinationServerUrl={2}", ravenFileSystemClient.ServerUrl, Uri.EscapeDataString(fileName), Uri.EscapeDataString(destinationServerUrl));
				var request = (HttpWebRequest)WebRequest.Create(requestUriString);
				request.Method = "POST";
				request.ContentLength = 0;
				return request.GetResponseAsync()
					.ContinueWith(task =>
					{
						using (var stream = task.Result.GetResponseStream())
						{
							return new JsonSerializer().Deserialize<SynchronizationReport>(new JsonTextReader(new StreamReader(stream)));
						}
					})
					.TryThrowBetterError();
			}

            public Task<SynchronizationReport> GetSynchronizationStatusAsync(string fileName)
            {
                var requestUriString = String.Format("{0}/synchronization/status/{1}", ravenFileSystemClient.ServerUrl, Uri.EscapeDataString(fileName));
                var request = (HttpWebRequest)WebRequest.Create(requestUriString.NoCache());
                request.ContentLength = 0;
                return request.GetResponseAsync()
                    .ContinueWith(task =>
                    {
                        using (var stream = task.Result.GetResponseStream())
                        {
                            return new JsonSerializer().Deserialize<SynchronizationReport>(new JsonTextReader(new StreamReader(stream)));
                        }
                    })
					.TryThrowBetterError();
            }

            public Task ResolveConflictAsync(string filename, ConflictResolutionStrategy strategy)
            {
                var requestUriString = String.Format("{0}/synchronization/resolveConflict/{1}?strategy={2}",
                    ravenFileSystemClient.ServerUrl, Uri.EscapeDataString(filename), Uri.EscapeDataString(strategy.ToString()));
                var request = (HttpWebRequest)WebRequest.Create(requestUriString);
                request.Method = "PATCH";
                return request.GetResponseAsync()
                    .ContinueWith(task => task.Result.Close())
					.TryThrowBetterError();
            }

            public Task ApplyConflictAsync(string filename, long remoteVersion, string remoteServerId, IList<HistoryItem> remoteHistory)
            {
                var requestUriString = String.Format("{0}/synchronization/applyConflict/{1}?remoteVersion={2}&remoteServerId={3}",
                    ravenFileSystemClient.ServerUrl, Uri.EscapeDataString(filename), remoteVersion, Uri.EscapeDataString(remoteServerId));
                var request = (HttpWebRequest)WebRequest.Create(requestUriString);
                request.Method = "PATCH";

	            return request.GetRequestStreamAsync()
		            .ContinueWith(
			            task =>
			            {
				            var sb = new StringBuilder();
				            var jw = new JsonTextWriter(new StringWriter(sb));
				            new JsonSerializer().Serialize(jw, remoteHistory);
				            var bytes = Encoding.UTF8.GetBytes(sb.ToString());

				            return task.Result.WriteAsync(bytes, 0, bytes.Length)
					            .ContinueWith(t => task.Result.Close())
					            .ContinueWith(t =>
					            {
						            t.Wait();
						            return request.GetResponseAsync();
					            })
					            .Unwrap()
					            .ContinueWith(t => t.Result.Close());
			            })
		            .Unwrap()
		            .TryThrowBetterError();
            }

            public Task<ListPage<SynchronizationReport>> GetFinishedAsync(int page = 0, int pageSize = 25)
            {
                var requestUriString = String.Format("{0}/synchronization/finished?start={1}&pageSize={2}", ravenFileSystemClient.ServerUrl, page, pageSize);
                var request = (HttpWebRequest)WebRequest.Create(requestUriString.NoCache());
                request.ContentLength = 0;
                return request.GetResponseAsync()
                    .ContinueWith(task =>
                    {
                        using (var stream = task.Result.GetResponseStream())
                        {
                            var preResult = new JsonSerializer().Deserialize<ListPage<SynchronizationReport>>(new JsonTextReader(new StreamReader(stream)));
                            return preResult;
                        }
                    })
					.TryThrowBetterError();
            }

            public Task<ListPage<SynchronizationDetails>> GetActiveAsync(int page = 0, int pageSize = 25)
            {
                var requestUriString = String.Format("{0}/synchronization/active?start={1}&pageSize={2}", ravenFileSystemClient.ServerUrl, page, pageSize);
                var request = (HttpWebRequest)WebRequest.Create(requestUriString.NoCache());
                request.ContentLength = 0;
                return request.GetResponseAsync()
                    .ContinueWith(task =>
                    {
                        using (var stream = task.Result.GetResponseStream())
                        {
                            var preResult = new JsonSerializer().Deserialize<ListPage<SynchronizationDetails>>(new JsonTextReader(new StreamReader(stream)));
                            return preResult;
                        }
                    })
					.TryThrowBetterError();
            }

            public Task<ListPage<SynchronizationDetails>> GetPendingAsync(int page = 0, int pageSize = 25)
			{
				var requestUriString = String.Format("{0}/synchronization/pending?start={1}&pageSize={2}", ravenFileSystemClient.ServerUrl, page, pageSize);
				var request = (HttpWebRequest)WebRequest.Create(requestUriString.NoCache());
				request.ContentLength = 0;
				return request.GetResponseAsync()
					.ContinueWith(task =>
					{
						using (var stream = task.Result.GetResponseStream())
						{
                            var preResult = new JsonSerializer().Deserialize<ListPage<SynchronizationDetails>>(new JsonTextReader(new StreamReader(stream)));
							return preResult;
						}
					})
					.TryThrowBetterError();
			}

			public Task<SourceSynchronizationInformation> GetLastSynchronizationFromAsync(Guid serverId)
			{
				var requestUriString = String.Format("{0}/synchronization/LastSynchronization?from={1}", ravenFileSystemClient.ServerUrl, serverId);
				var request = (HttpWebRequest)WebRequest.Create(requestUriString.NoCache());
				request.ContentLength = 0;
				return request.GetResponseAsync()
					.ContinueWith(task =>
					{
						using (var stream = task.Result.GetResponseStream())
						{
							var preResult = new JsonSerializer().Deserialize<SourceSynchronizationInformation>(new JsonTextReader(new StreamReader(stream)));
							return preResult;
						}
					})
					.TryThrowBetterError();
			}

			public Task<IEnumerable<SynchronizationConfirmation>> ConfirmFilesAsync(IEnumerable<Tuple<string, Guid>> sentFiles)
			{
				var requestUriString = String.Format("{0}/synchronization/Confirm", ravenFileSystemClient.ServerUrl);
				var request = (HttpWebRequest)WebRequest.Create(requestUriString);
				request.ContentType = "application/json";
				request.Method = "POST";

				return request.GetRequestStreamAsync()
					.ContinueWith(
						task =>
							{
								var sb = new StringBuilder();
								var jw = new JsonTextWriter(new StringWriter(sb));
								new JsonSerializer().Serialize(jw, sentFiles);
								var bytes = Encoding.UTF8.GetBytes(sb.ToString());

								return task.Result.WriteAsync(bytes, 0, bytes.Length)
									.ContinueWith(t => task.Result.Close())
									.ContinueWith(t =>
									              	{
									              		t.Wait();
									              		return request.GetResponseAsync();
									              	})
									.Unwrap()
									.ContinueWith(t =>
									              	{
									              		using (var stream = t.Result.GetResponseStream())
									              		{
									              			return
									              				new JsonSerializer().Deserialize<IEnumerable<SynchronizationConfirmation>>(
									              					new JsonTextReader(new StreamReader(stream)));
									              		}
									              	});
							})
					.Unwrap()
					.TryThrowBetterError();
			}

			public Task<ListPage<ConflictItem>> GetConflicts(int page = 0, int pageSize = 25)
			{
				var requestUriString = String.Format("{0}/synchronization/conflicts?start={1}&pageSize={2}", ravenFileSystemClient.ServerUrl, page, pageSize);
				var request = (HttpWebRequest)WebRequest.Create(requestUriString.NoCache());
				request.ContentLength = 0;
				return request.GetResponseAsync()
					.ContinueWith(task =>
					{
						using (var stream = task.Result.GetResponseStream())
						{
							var preResult = new JsonSerializer().Deserialize<ListPage<ConflictItem>>(new JsonTextReader(new StreamReader(stream)));
							return preResult;
						}
					})
					.TryThrowBetterError();
			}

			public Task IncrementLastETagAsync(Guid sourceServerId, Guid sourceFileETag)
			{
				var requestUriString = String.Format("{0}/synchronization/IncrementLastETag?sourceServerId={1}&sourceFileETag={2}",
				                                     ravenFileSystemClient.ServerUrl, sourceServerId, sourceFileETag);
				var request = (HttpWebRequest) WebRequest.Create(requestUriString.NoCache());
				request.ContentLength = 0;
				request.Method = "POST";
				return request.GetResponseAsync()
					.ContinueWith(task => task.Result.Close())
					.TryThrowBetterError();
			}
		}

		public Task<RdcStats> GetRdcStatsAsync()
		{
			var requestUriString = ServerUrl + "/rdc/stats";
			var request = (HttpWebRequest)WebRequest.Create(requestUriString.NoCache());
			return request.GetResponseAsync()
				.ContinueWith(task =>
				{
					using (var stream = task.Result.GetResponseStream())
					{
						return new JsonSerializer().Deserialize<RdcStats>(new JsonTextReader(new StreamReader(stream)));
					}
				})
				.TryThrowBetterError();
		}

	    public void Dispose()
	    {
	        notifications.Dispose();
	    }
	}
}
