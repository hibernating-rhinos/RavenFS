using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web;
using RavenFS.Util;

namespace RavenFS.Handlers
{
	[HandlerMetadata(@"/files/(.+)", "PUT")]
	public class PutFileHandler : AbstractAsyncHandler
	{
		protected override Task ProcessRequestAsync(HttpContext context)
		{
			var filename = Url.Match(context.Request.Url.AbsolutePath).Captures[1].Value;
			var state = new ReadState {Buffer = TakeBuffer()};

			return Read(filename, state, context, new List<HashKey>(), 0)
				.ContinueWith(task =>
				{
					BufferPool.ReturnBuffer(state.Buffer);
					return task;
				}).Unwrap();
		}

		private Task Read(string filename, ReadState state, HttpContext context, List<HashKey> savedPages, long total)
		{
			return context.Request.InputStream.ReadAsync(state.Buffer, state.Read, state.Buffer.Length - state.Read)
				.ContinueWith(task =>
				{
					if (task.Result == 0) // done reading all
					{
						if (state.Read != 0)
						{
							savedPages.Add(Storage.InsertPage(state.Buffer, 0, state.Read));
						}

						Storage.PutFile(filename, total, savedPages);

						return task;
					}
					total += task.Result;
					state.Read = +task.Result;
					if (state.Read == state.Buffer.Length) // done reading page
					{
						savedPages.Add(Storage.InsertPage(state.Buffer, 0, state.Read));
						state.Read = 0;
					}
					return Read(filename, state, context, savedPages, total);
				})
				.Unwrap();
		}

		public class ReadState
		{
			public byte[] Buffer;
			public int Read;
		}
	}
}