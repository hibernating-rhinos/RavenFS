using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Text;
using System.Linq;

namespace RavenFS.Client
{
	public class MultiPartParser
	{
		private readonly byte[] boundaryBytes;

		public string ContentType { get; private set; }

		public Stream InputStream { get; private set; }

		public Encoding ContentEncoding { get; private set; }

		private readonly StringBuilder currentLine = new StringBuilder();

		public MultiPartParser(string contentType, Stream inputStream, Encoding contentEncoding)
		{
			ContentType = contentType;
			InputStream = inputStream;
			ContentEncoding = contentEncoding;

			string boundary = GetParameter(ContentType, "; boundary=");
			if (boundary == null)
				throw new InvalidOperationException("Could not figure out what the boundary is");

			boundaryBytes = ContentEncoding.GetBytes("--" + boundary);
		}

		public Tuple<Stream,NameValueCollection> Next()
		{
			while (true)
			{
				if (ReadUntilBoundary() == false)
					return null;
				var headers = ReadHeaders();
				var contentDisposition = headers.Get("Content-Disposition");
				if (contentDisposition == null || !contentDisposition.Contains("filename="))
				{
					continue;
				}

				return Tuple.Create<Stream,NameValueCollection>(new BoundedStream(InputStream, boundaryBytes), headers);
			}
		}

		private NameValueCollection ReadHeaders()
		{
			var headers = new NameValueCollection();
			ReadLine(); // consume crlf after boundary
			while (true)
			{
				string line = ReadLine();
				if (string.IsNullOrEmpty(line))
					break;
				var indexOfColon = line.IndexOf(": ");
				if (indexOfColon == -1)
					throw new InvalidOperationException("Header without : field");

				headers[line.Substring(0, indexOfColon)] = line.Substring(indexOfColon + 1);
			}

			return headers;
		}

		private string ReadLine()
		{
			currentLine.Length = 0;
			int lastChar = -1;
			while (true)
			{
				int currentChar = InputStream.ReadByte();
				switch (currentChar)
				{
					case -1:
						return null;
					case '\n':
						if (lastChar == '\r')
						{
							currentLine.Length--;
						}
						return currentLine.ToString();
					default:
						currentLine.Append((char)currentChar);
						break;
				}
				lastChar = currentChar;
			}
		}

		private bool ReadUntilBoundary()
		{
			int boundaryPosition = 0;
			int ch;
			while ((ch = InputStream.ReadByte()) != -1)
			{
				if (boundaryBytes[boundaryPosition] == (byte)ch)
				{
					boundaryPosition += 1;
					if (boundaryPosition == boundaryBytes.Length)
						return true; // done
				}
				else
				{
					boundaryPosition = 0;
				}
			}
			return false;
		}


		static internal string GetParameter(string header, string attr)
		{
			int ap = header.IndexOf(attr);
			if (ap == -1)
				return null;

			ap += attr.Length;
			if (ap >= header.Length)
				return null;

			char ending = header[ap];
			if (ending != '"')
				ending = ' ';

			int end = header.IndexOf(ending, ap + 1);
			if (end == -1)
				return (ending == '"') ? null : header.Substring(ap);

			return header.Substring(ap + 1, end - ap - 1);
		}


		public class BoundedStream : Stream
		{
			private readonly Stream inner;
			private readonly byte[] boundary;
			private bool done;
			private byte[] nextBuffer;
			public BoundedStream(Stream inner, byte[] boundary)
			{
				this.inner = inner;
				this.boundary = boundary;
			}

			public override void Flush()
			{
				throw new NotSupportedException();
			}

			public override long Seek(long offset, SeekOrigin origin)
			{
				throw new NotSupportedException();
			}

			public override void SetLength(long value)
			{
				throw new NotSupportedException();
			}

			public override int Read(byte[] buffer, int offset, int count)
			{
				if (done)
					return 0;
				if(nextBuffer != null) // we have some stuff remaining from previous call
				{
					Buffer.BlockCopy(nextBuffer, 0, buffer, offset, count);
					var length = nextBuffer.Length;
					if(length <= count)
					{
						nextBuffer = null;
						return length;
					}
					// not enough in the buffer, need to chop the stuff we already have there
					// and leave the rest for the next call
					nextBuffer = nextBuffer.Skip(count).ToArray();
					return count;
				}
				var read = inner.Read(buffer, offset, count);
				if (read == 0)
					return read;

				int boundaryIndex = 0;
				for (var i = offset; i < count; i++)
				{
					if (boundary[boundaryIndex] != buffer[i])
					{
						boundaryIndex = 0;
					}
					else
					{
						boundaryIndex += 1;
						if (boundaryIndex == boundary.Length)
						{
							done = true;
							return i-offset - boundary.Length; // we got a boundary, we are done for this stream
						}
					}
				}

				if (boundaryIndex == 0)
					return read;

				// this is where it gets complex, we found a partial match in the buffer,
				// but we don't have enough data to know if we are done or if this is just 
				// accidental match

				var remainingBuffer = new List<byte>();
				for (int i = boundaryIndex; i < boundary.Length; i++)
				{
					var nextByte = inner.ReadByte();
					if(nextByte != -1)
						remainingBuffer.Add((byte)nextByte);

					if (nextByte != boundary[i])
					{
						nextBuffer = remainingBuffer.ToArray();
						return read;
					}
				}
				done = true;
				return read;
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
				get { return false; }
			}

			public override bool CanWrite
			{
				get { return false; }
			}

			public override long Length
			{
				get { throw new NotSupportedException(); }
			}

			public override long Position
			{
				get { throw new NotSupportedException(); }
				set { throw new NotSupportedException(); }
			}
		}
	}
}