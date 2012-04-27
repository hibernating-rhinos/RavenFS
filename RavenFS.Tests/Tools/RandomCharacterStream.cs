namespace RavenFS.Tests.Tools
{
	using System;
	using System.IO;

	public class RandomCharacterStream: Stream
    {
        private long _length;
        private long _position;
        private Random _random;

		public RandomCharacterStream(long length, int? seed = null)
        {
            _length = length;
            if (seed != null)
            {
                _random = new Random(seed.Value);
            }
            else
            {
                _random = new Random();
            }
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

		private const string _chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuwvxyz";

        public override int Read(byte[] buffer, int offset, int count)
        {
            var length = Math.Min(_length - _position, count);
            if (length < 1)
            {
                return 0;
            }
            var newValues = new byte[length];
            for (int i = 0; i < length; i++)
			{
				newValues[i] = (byte) _chars[_random.Next(_chars.Length)];
			}

            Array.Copy(newValues, 0, buffer, offset, length);
            _position += length;
            return Convert.ToInt32(length);
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
            get { return _length; }
        }

        public override long Position
        {
            get { return _position; }
            set { throw new NotSupportedException(); }
        }
	}
}