using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RavenFS.Tests.Tools
{
    public class RandomStream : Stream
    {
        private long _length;
        private long _position;
        private Random _random;

        public RandomStream(long length, int? seed = null)
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

        public override int Read(byte[] buffer, int offset, int count)
        {
            var length = Math.Min(_length - _position, count);
            if (length < 1)
            {
                return 0;
            }
            var newValues = new byte[length];

			// TODO: Temporary solution to avoid multipart parsing problems
			// if characters with special meaning for parser (\r \n) occur in body part.
			// Next version of WepApi will fix that issue:
			// http://aspnetwebstack.codeplex.com/SourceControl/changeset/changes/fc9958338137

            //_random.NextBytes(newValues);

        	const byte carriageReturn = 0xd;
        	const byte newLine = 0xa;

        	for (int i = 0; i < newValues.Length; i++)
        	{
        		newValues[i] = (byte) _random.Next(byte.MaxValue);
        		
				if(newValues[i] == carriageReturn || newValues[i] == newLine)
				{
					newValues[i] = (byte) _random.Next(carriageReturn + 1, byte.MaxValue);
				}
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
