using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RavenFS.Client
{
    internal class ListenableStream : Stream
    {
        public class ProgressEventArgs : EventArgs
        {
            public long Processed { get; private set; }

            public ProgressEventArgs(long processed)
            {
                Processed = processed;
            }
        }

        private readonly Stream source;
        private long alreadyRead;
	    private long alreadyWritten;
	    private long readCount;
	    private long writeCount;

        public event EventHandler<ProgressEventArgs> ReadingProgress;

        public void InvokeReadingProgress(ProgressEventArgs e)
        {
            var handler = ReadingProgress;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        public event EventHandler<ProgressEventArgs> WrittingProgress;

        public void InvokeWrittingProgress(ProgressEventArgs e)
        {
            var handler = WrittingProgress;
            if (handler != null)
            {
                handler(this, e);
            }

        }

        public ListenableStream(Stream source)
        {
            this.source = source;
        }

        public override void Flush()
        {
            source.Flush();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return source.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            source.SetLength(value);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var result = source.Read(buffer, offset, count);
            alreadyRead += result;
			
			if(readCount % 10 == 0 || readCount < 100)
				InvokeReadingProgress(new ProgressEventArgs(alreadyRead));
	        readCount++;

            return result;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            source.Write(buffer, offset, count);
            alreadyWritten += count;

			if(writeCount % 10 == 0 || readCount < 100)
				InvokeWrittingProgress(new ProgressEventArgs(alreadyWritten));
	        writeCount++;
        }

        public override bool CanRead
        {
            get { return source.CanRead; }
        }

        public override bool CanSeek
        {
            get { return source.CanSeek; }
        }

        public override bool CanWrite
        {
            get { return source.CanWrite; }
        }

        public override long Length
        {
            get { return source.Length; }
        }

        public override long Position
        {
            get { return source.Position; }
            set { source.Position = value; }
        }
    }
}
