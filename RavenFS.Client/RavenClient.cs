using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace RavenFS.Client
{
	public class RavenClient
	{
		private readonly string baseUrl;

		public RavenClient(string baseUrl)
		{
			this.baseUrl = baseUrl;
			if (this.baseUrl.EndsWith("/"))
				this.baseUrl = this.baseUrl.Substring(0, this.baseUrl.Length - 1);
		}

		public Task<FileInfo[]> Search(string query)
		{
			var request = (HttpWebRequest) WebRequest.Create(baseUrl + "/search?query=" + Uri.EscapeUriString(query));
			return request.GetResponseAsync()
				.ContinueWith(task =>
				{
						using(var responseStream = task.Result.GetResponseStream())
						using(var streamReader = new StreamReader(responseStream))
						using(var jsonTextReader = new JsonTextReader(streamReader))
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

		public Task<NameValueCollection> Head(string filename)
		{
			var request = (HttpWebRequest)WebRequest.Create(baseUrl + "/files/" + filename);
			request.Method = "HEAD";
			return request.GetResponseAsync()
				.ContinueWith(task => new NameValueCollection(task.Result.Headers));
		}

		public Task<NameValueCollection> Download(string filename, Stream destination)
		{
			return Download(filename, new NameValueCollection(), destination);
		}

		public Task<NameValueCollection> Download(string filename, NameValueCollection collection, Stream destination)
		{
			if (destination.CanWrite == false)
				throw new ArgumentException("Stream does not support writing");

			var request = (HttpWebRequest)WebRequest.Create(baseUrl + "/files/" + filename);


			if (destination.CanSeek)
			{
				destination.Position = destination.Length;
				request.AddRange(destination.Position);
			}

			return request.GetResponseAsync()
				.ContinueWith(task =>
				{
					var responseStream = task.Result.GetResponseStream();
					return responseStream.CopyToAsync(destination)
						.ContinueWith(_ =>
						{
							task.Result.Close();
							return new NameValueCollection(task.Result.Headers);
						});
				})
				.Unwrap();
		}

		public Task Upload(string filename, Stream source)
		{
			return Upload(filename, new NameValueCollection(), source);
		}

		public Task Upload(string filename, NameValueCollection collection, Stream source)
		{
			if (source.CanRead == false)
				throw new AggregateException("Stream does not support reading");

			var request = (HttpWebRequest)WebRequest.Create(baseUrl + "/files/" + filename);
			request.Method = "PUT";

			foreach (var key in collection.AllKeys)
			{
				var values = collection.GetValues(key);
				if (values == null)
					continue;
				foreach (var value in values)
				{
					request.Headers.Add(key, value);
				}
			}


			return request.GetRequestStreamAsync()
				.ContinueWith(task => source.CopyToAsync(task.Result)
										.ContinueWith(_ => task.Result.Close())
				)
				.Unwrap()
				.ContinueWith(task => request.GetResponseAsync())
				.Unwrap()
				.ContinueWith(task => task.Result.Close());
		}
	}
}