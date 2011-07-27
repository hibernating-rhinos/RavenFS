using System.Threading.Tasks;
using System.Web;

namespace RavenFS.Extensions
{
	public static class HttpContextAsyncExtensions
	{
		public static Task WriteAsync(this HttpContext context, byte[] bytes)
		{
			return Task.Factory.FromAsync(context.Response.OutputStream.BeginWrite,
								   context.Response.OutputStream.EndWrite, bytes, 0, bytes.Length, null);
		}
	}
}