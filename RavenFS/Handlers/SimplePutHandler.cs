using System;
using System.IO;
using System.Threading.Tasks;
using System.Web;
using RavenFS.Infrastructure;
using RavenFS.Extensions;
using Raven.Abstractions.Extensions;

namespace RavenFS.Handlers
{
	[HandlerMetadata("^/files/(.+)", "PUT")]
	public class SimplePutHandler : AbstractAsyncHandler
	{
		protected override Task ProcessRequestAsync(HttpContext context)
		{
			var filename = Url.Match(context.Request.Url.AbsolutePath).Groups[1].Value;
			
			Storage.Batch(accessor =>
			{
				accessor.Delete(filename);

				var headers = context.Request.Headers.FilterHeaders();
				accessor.PutFile(filename, context.Request.ContentLength,
								 headers);

				Search.Index(filename, headers);
			});

			var readFileToDatabase = new ReadFileToDatabase(this, context, filename);
			try
			{
				return readFileToDatabase.Execute()
					.ContinueWith(task =>
					{
						readFileToDatabase.Dispose();
						return task;
					})
					.Unwrap();
			}
			catch (Exception)
			{
				readFileToDatabase.Dispose();

				throw;
			}
		}

		public class ReadFileToDatabase : IDisposable
		{
			private readonly AbstractAsyncHandler parent;
			private readonly HttpContext context;
			private readonly string filename;
			private int pos;
			readonly byte[] buffer;
			private Stream inputStream;

			public ReadFileToDatabase(AbstractAsyncHandler parent, HttpContext context, string filename)
			{
				this.parent = parent;
				this.context = context;
				this.filename = filename;
				buffer = parent.TakeBuffer();
				inputStream = context.Request.GetBufferlessInputStream();
			}

			public Task Execute()
			{
				return inputStream.ReadAsync(buffer)
					.ContinueWith(task =>
					{
						if (task.Result == 0) // nothing left to read
							return parent.Completed;

						parent.Storage.Batch(accessor =>
						{
							var hashKey = accessor.InsertPage(buffer, task.Result);
							accessor.AssociatePage(filename, hashKey, pos, task.Result);
						});

						pos++;
						return Execute();
					})
					.Unwrap();
			}

			public void Dispose()
			{
				parent.BufferPool.ReturnBuffer(buffer);
			}
		}

	}
}