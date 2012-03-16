using System;
using System.Diagnostics;
using System.IO;
using RavenFS.Storage;
using RavenFS.Util;

namespace RavenFS.Infrastructure
{
	public class RavenReadOnlyStream : Stream
	{
		private readonly TransactionalStorage storage;
		private readonly BufferPool bufferPool;
		private readonly string filename;
		private FileAndPages header;

		private long position;
		private byte[] internalBuffer;
		private int internalBufferSize; // note that it may be smaller than internalBuffer.Length
		private int posInBuffer;

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
					position = offset;
					if(offset > Length)
						throw new ArgumentException("New offset would be bigger than the file size");
					break;
				case SeekOrigin.Current:
					if (position + offset > Length)
						throw new ArgumentException("New offset would be bigger than the file size");
					position += offset;
					break;
				case SeekOrigin.End:
					if (Length - offset < 0)
						throw new ArgumentException("New offset would be before start of file");
					position = Length - offset;
					break;
				default:
					throw new ArgumentOutOfRangeException("origin");
			}
			return position;
		}

		public override void SetLength(long value)
		{
			throw new NotSupportedException();
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			if(internalBuffer != null && posInBuffer < internalBufferSize )
			{
				// serve directly from loaded buffer
				int readFromBuffer = Math.Min(count, internalBufferSize - posInBuffer);
				Buffer.BlockCopy(internalBuffer, posInBuffer, buffer, offset, readFromBuffer);
				posInBuffer += readFromBuffer;
				position += readFromBuffer;
				return readFromBuffer;
			}
			// need to check for a new buffer
			var tuple = GetPageFor(position);
			if(tuple.Item1 == null)
				return 0;

			ReturnBuffer();
			TakeBuffer(tuple.Item1.Size);
				
			storage.Batch(accessor => accessor.ReadPage(tuple.Item1.Id, internalBuffer));

			Debug.Assert(internalBuffer != null);

			int read = Math.Min(count, internalBufferSize - tuple.Item2);
			Buffer.BlockCopy(internalBuffer, tuple.Item2, buffer, offset, read);
			
			position += read;
			posInBuffer = tuple.Item2 + read;
			return read;
		}

		private void TakeBuffer(int size)
		{
			internalBufferSize = size;
			internalBuffer = bufferPool.TakeBuffer(size);
		}

		private void ReturnBuffer()
		{
			posInBuffer = 0;
			if(internalBuffer != null)
			{
				bufferPool.ReturnBuffer(internalBuffer);
				internalBuffer = null;
			}
		}

		private Tuple<PageInformation,int> GetPageFor(long totalPos)
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
						if (current <= totalPos && totalPos < current + pageInformation.Size)
						{
							page = pageInformation;
							posInPage = (int)(totalPos - current);
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

		public override long Position
		{
			get { return position; }
			set { Seek(value, SeekOrigin.Begin); }
		}

		protected override void Dispose(bool disposing)
		{
			if (internalBuffer != null)
				bufferPool.ReturnBuffer(internalBuffer);
			base.Dispose(disposing);
		}
	}
}