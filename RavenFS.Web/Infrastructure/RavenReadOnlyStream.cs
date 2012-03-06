using System;
using System.IO;
using RavenFS.Storage;
using RavenFS.Util;

namespace RavenFS.Web.Infrastructure
{
	public class RavenReadOnlyStream : Stream
	{
		private readonly TransactionalStorage storage;
		private readonly BufferPool bufferPool;
		private readonly string filename;
		private FileAndPages header;

		public RavenReadOnlyStream(TransactionalStorage storage, BufferPool bufferPool, string filename)
		{
			this.storage = storage;
			this.bufferPool = bufferPool;
			this.filename = filename;

			storage.Batch(accessor =>
			{
				header = accessor.GetFile(filename, 0, 0);
			});
		}

		public override void Flush()
		{
			throw new NotSupportedException();
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			switch (origin)
			{
				case SeekOrigin.Begin:
					Position = offset;
					if(offset > Length)
						throw new ArgumentException("New offset would be bigger than the file size");
					break;
				case SeekOrigin.Current:
					if (Position + offset > Length)
						throw new ArgumentException("New offset would be bigger than the file size");
					Position += offset;
					break;
				case SeekOrigin.End:
					if (Length - offset < 0)
						throw new ArgumentException("New offset would be before start of file");
					Position = Length - offset;
					break;
				default:
					throw new ArgumentOutOfRangeException("origin");
			}
			return Position;
		}

		public override void SetLength(long value)
		{
			throw new NotSupportedException();
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			var tuple = GetPageFor(Position);
			if(tuple.Item1 == null)
				return 0;

			int read = 0;
			storage.Batch(accessor =>
			{
				var pageBuffer = bufferPool.TakeBuffer(tuple.Item1.Size);
				try
				{
					accessor.ReadPage(tuple.Item1.Key, pageBuffer);
					read = Math.Min(count, pageBuffer.Length - tuple.Item2);
					Buffer.BlockCopy(pageBuffer, tuple.Item2, buffer, offset, read);
				}
				finally
				{
					bufferPool.ReturnBuffer(pageBuffer);
				}
			});
			Position += read;
			return read;
		}

		private Tuple<PageInformation,int> GetPageFor(long position)
		{
			long current = 0;
			var start = 0;
			PageInformation page = null;
			int posInPage = 0;
			storage.Batch(accessor =>
			{
				while (true)
				{
					var fileAndPages = accessor.GetFile(filename, start, 256);

					if (fileAndPages.Pages.Count == 0)
						return;

					start += 256;

					foreach (var pageInformation in fileAndPages.Pages)
					{
						if (current >= position && current <= position + pageInformation.Size)
						{
							page = pageInformation;
							posInPage = (int)(position - current);
							return;
						}
						current += pageInformation.Size;
					}
				}
			});

			return Tuple.Create(page, posInPage);
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			throw new NotSupportedException();
		}

		public override bool CanRead
		{
			get { return true; }
		}

		public override bool CanSeek
		{
			get { return true; }
		}

		public override bool CanWrite
		{
			get { return false; }
		}

		public override long Length
		{
			get
			{
				return Math.Abs(header.UploadedSize);
			}
		}

		public override long Position { get; set; }
	}
}