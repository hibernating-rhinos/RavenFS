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
			var filename = Url.Match(context.Request.CurrentExecutionFilePath).Groups[1].Value;
			
			Storage.Batch(accessor =>
			{
				accessor.Delete(filename);

				var headers = context.Request.Headers.FilterHeaders();
				var contentLength = context.Request.ContentLength;
				if (context.Request.Headers["Transfer-Encoding"] == "chunked")
					contentLength = -1;
				accessor.PutFile(filename,
								 contentLength, 
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
			private readonly string filename;
			private int pos;
			readonly byte[] buffer;
			private readonly Stream inputStream;

			public ReadFileToDatabase(AbstractAsyncHandler parent, HttpContext context, string filename)
			{
				this.parent = parent;
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
						{
							parent.Storage.Batch(accessor =>
							{
								accessor.CompleteFileUpload(filename);
							});
							return parent.Completed;
						}

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