namespace RavenFS.Infrastructure
{
	using System.Net.Http;
	using System.Net.Http.Headers;
	using System.Threading;
	using System.Threading.Tasks;

	public class CachePreventingHandler : DelegatingHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(
			HttpRequestMessage request, CancellationToken cancellationToken)
		{
			return base.SendAsync(request, cancellationToken).ContinueWith(
				task =>
				{
					HttpResponseMessage response = task.Result;

					if (response.Headers != null)
					{
						if (response.Headers.CacheControl == null)
						{
							response.Headers.CacheControl = new CacheControlHeaderValue();
						}

						response.Headers.CacheControl.NoCache = true;
					}

					return response;
				}
			);
		}
	}
}