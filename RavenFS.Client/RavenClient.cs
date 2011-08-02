using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace RavenFS.Client
{
	public class RavenClient
	{
		private readonly string baseUrl;

		public RavenClient(string baseUrl)
		{
			this.baseUrl = baseUrl;
			if(this.baseUrl.EndsWith("/"))
				this.baseUrl = this.baseUrl.Substring(0, this.baseUrl.Length - 1);
		}

		public Task Download(string filename, Stream destination)
		{
			if(destination.CanWrite == false)
				throw new ArgumentException("Stream does not support writing");

			var request = (HttpWebRequest)WebRequest.Create(baseUrl + "/files/" + filename);

			if(destination.CanSeek)
			{
				destination.Position = destination.Length;
				request.AddRange(destination.Position);	
			}

			return request.GetResponseAsync()
				.ContinueWith(task =>
				{
					var responseStream = task.Result.GetResponseStream();
					return responseStream.CopyToAsync(destination)
						.ContinueWith(_ => task.Result.Close());
				})
				.Unwrap();
		}

		public Task Upload(string filename, Stream source)
		{
			if(source.CanRead == false)
				throw new AggregateException("Stream does not support reading");

			var request = (HttpWebRequest)WebRequest.Create(baseUrl + "/files/" + filename);
			request.Method = "PUT";
			return request.GetRequestStreamAsync()
				.ContinueWith(task => source.CopyToAsync(task.Result)
				                      	.ContinueWith(_ => task.Result.Close())
				)
				.Unwrap()
				.ContinueWith(task => request.GetResponseAsync())
				.Unwrap();
		}
	}
}