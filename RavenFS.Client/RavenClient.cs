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
			var request = (HttpWebRequest)WebRequest.Create(baseUrl + "/files/" + filename);
			destination.Position = destination.Length;
			request.AddRange(destination.Position);
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
			var request = (HttpWebRequest)WebRequest.Create(baseUrl + "/files/" + filename);
			request.Method = "PUT";
			return request.GetRequestStreamAsync()
				.ContinueWith(task => source.CopyToAsync(task.Result)
				                      	.ContinueWith(_ => task.Result.Close())
				)
				.ContinueWith(task => request.GetResponseAsync())
				.Unwrap();
		}
	}
}