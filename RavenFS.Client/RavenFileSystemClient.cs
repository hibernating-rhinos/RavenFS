using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
#if SILVERLIGHT
using System.Net.Browser;
#endif
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace RavenFS.Client
{
	public class RavenFileSystemClient
	{
		private readonly string baseUrl;

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
			if (this.baseUrl.EndsWith("/"))
				this.baseUrl = this.baseUrl.Substring(0, this.baseUrl.Length - 1);
		}

		public Task<ServerStats> StatsAsync()
		{
			var requestUriString = baseUrl + "/stats";
			var request = (HttpWebRequest)WebRequest.Create(requestUriString);
			return request.GetResponseAsync()
				.ContinueWith(task =>
				{
					using (var stream = task.Result.GetResponseStream())
					{
						return new JsonSerializer().Deserialize<ServerStats>(new JsonTextReader(new StreamReader(stream)));
					}
				});
		}

		public Task DeleteAsync(string filename)
		{
			var requestUriString = baseUrl + "/files/" + Uri.EscapeDataString(filename);
			var request = (HttpWebRequest)WebRequest.Create(requestUriString);
			request.Method = "DELETE";
			return request.GetResponseAsync()
				.ContinueWith(task => task.Result.Close());
		}

		public Task<FileInfo[]> BrowseAsync(int start = 0, int pageSize = 25)
		{
			var request = (HttpWebRequest)WebRequest.Create(baseUrl + "/files?start=" + start + "&pageSize=" + pageSize);
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
				});
		}

		public Task<FileInfo[]> SearchAsync(string query)
		{
			var request = (HttpWebRequest)WebRequest.Create(baseUrl + "/search?query=" + Uri.EscapeUriString(query));
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
				});
		}

		public Task<NameValueCollection> GetMetadataForAsync(string filename)
		{
			var request = (HttpWebRequest)WebRequest.Create(baseUrl + "/files/" + filename);
			request.Method = "HEAD";
			return request.GetResponseAsync()
				.ContinueWith(task => new NameValueCollection(task.Result.Headers));
		}

		public Task<NameValueCollection> DownloadAsync(string filename, Stream destination)
		{
			return DownloadAsync("/files/", filename, destination);
		}

        public Task<NameValueCollection> DownloadAsync(string filename, Stream destination, long from, long to)
        {
            return DownloadAsync("/files/", filename, destination, new Tuple<long, long?>(from, to));
        }

		public Task<NameValueCollection> DownloadAsync(string path, string filename, Stream destination, Tuple<long, long?> fromTo = null,
            Action<string, int> progress = null)
		{
#if SILVERLIGHT
            if (fromTo != null)
            {
                throw new NotSupportedException("Silverlight doesn't support partial requests");
            }
#endif

            var collection = new NameValueCollection();
			if (destination.CanWrite == false)
				throw new ArgumentException("Stream does not support writing");

			var request = (HttpWebRequest)WebRequest.Create(baseUrl + path + filename);

#if !SILVERLIGHT
            if (fromTo != null)
            {
                if (fromTo.Item2 != null)
                {
                    request.AddRange(fromTo.Item1, fromTo.Item2.Value);
                }
                else
                {
                    request.AddRange(fromTo.Item1);
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
				.Unwrap();
		}

	    public Task UpdateMetadataAsync(string filename, NameValueCollection metadata)
		{
			var request = (HttpWebRequest)WebRequest.Create(baseUrl + "/files/" + filename);
			
			request.Method = "POST";
			AddHeaders(metadata, request);
			return request
				.GetRequestStreamAsync()
				.ContinueWith(requestTask =>
				{
					if (requestTask.Exception != null)
						return requestTask;
					return request.GetResponseAsync()
						.ContinueWith(task =>
						{
							if (task.Result != null)
								task.Result.Close();
							return task;
						})
						.Unwrap();
				}).Unwrap();
		}

		public Task UploadAsync(string filename, Stream source)
		{
			return UploadAsync(filename, new NameValueCollection(), source, null);
		}

		public Task UploadAsync(string filename, NameValueCollection metadata, Stream source)
		{
			return UploadAsync(filename, metadata, source, null);
		}

		public Task UploadAsync(string filename, NameValueCollection metadata, Stream source, Action<string, int> progress)
		{
			if (source.CanRead == false)
				throw new AggregateException("Stream does not support reading");

			var request = (HttpWebRequest)WebRequest.Create(baseUrl + "/files/" + filename);
			request.Method = "PUT";
#if !SILVERLIGHT
			request.SendChunked = true;
			request.AllowWriteStreamBuffering = false;
#endif
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
                .ContinueWith(task =>
                {
                    task.Result.Close();
                });
		}

        public Task<RdcStats> GetRdcStatsAsync()
        {
            var requestUriString = baseUrl + "/rdc/stats";
            var request = (HttpWebRequest)WebRequest.Create(requestUriString);
            return request.GetResponseAsync()
                .ContinueWith(task =>
                {
                    using (var stream = task.Result.GetResponseStream())
                    {
                        return new JsonSerializer().Deserialize<RdcStats>(new JsonTextReader(new StreamReader(stream)));
                    }
                });
        }

        public Task<SignatureManifest> GetRdcManifestAsync(string path)
        {
            var requestUriString = baseUrl + "/rdc/manifest/" + StringUtils.UrlEncode(path);
            var request = (HttpWebRequest)WebRequest.Create(requestUriString);
            return request.GetResponseAsync()
                .ContinueWith(task =>
                {
                    using (var stream = task.Result.GetResponseStream())
                    {
                        return new JsonSerializer().Deserialize<SignatureManifest>(new JsonTextReader(new StreamReader(stream)));
                    }
                });
        }

        public Task DownloadSignatureAsync(string sigName, Stream destination)
        {
            return DownloadAsync("/rdc/signatures/", sigName, destination);
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

	    public Task<string> StartSynchronizationAsync(string serverIdentifier, string fileName)
	    {
            var requestUriString = baseUrl + "/synchronize/" + Uri.EscapeDataString(serverIdentifier) + "/" + Uri.EscapeDataString(fileName); ;
            var request = (HttpWebRequest)WebRequest.Create(requestUriString);
            return request.GetResponseAsync()
                .ContinueWith(task =>
                {
                    using (var stream = task.Result.GetResponseStream())
                    {
                        return new StreamReader(stream).ReadToEnd();
                    }
                });
	    }
	}    
}