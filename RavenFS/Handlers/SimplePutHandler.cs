using System.Threading.Tasks;
using System.Web;
using RavenFS.Infrastructure;
using RavenFS.Extensions;

namespace RavenFS.Handlers
{
	[HandlerMetadata("^/files/(.+)", "PUT")]
	public class SimplePutHandler : AbstractAsyncHandler
	{
		protected override Task ProcessRequestAsync(HttpContext context)
		{
			var filename = Url.Match(context.Request.Url.AbsolutePath).Groups[1].Value;

			Storage.Batch(accessor => accessor.PutFile(filename, context.Request.ContentLength));

			return ReadAllPages(context, filename, 0);
		}

		private Task ReadAllPages(HttpContext context, string filename, int pos)
		{
			return ReadPage(context, filename, pos)
				.ContinueWith(task =>
				{
					if (task.Result == false)
						return task;

					return ReadPage(context, filename, pos + 1);
				})
				.Unwrap();
		}

		private Task<bool> ReadPage(HttpContext context, string filename, int pos)
		{
			var buffer = TakeBuffer();
			return context.Request.InputStream.ReadAsync(buffer)
				.ContinueWith(task =>
				{
					if (task.Result == 0) // nothing left to read
						return false;

					Storage.Batch(accessor =>
					{
						var hashKey = accessor.InsertPage(buffer, task.Result);
						accessor.AssociatePage(filename, hashKey, pos, task.Result);
					});

					return true;
				});
		}
	}
}