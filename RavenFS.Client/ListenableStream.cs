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
            public int Processed { get; private set; }

            public ProgressEventArgs(int processed)
            {
                Processed = processed;
            }
        }

        private readonly Stream source;
        private int alreadyRead;
        private int alreadyWritten { get; set; }
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
            InvokeReadingProgress(new ProgressEventArgs(alreadyRead));
            return result;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            source.Write(buffer, offset, count);
            alreadyWritten += count;
            InvokeWrittingProgress(new ProgressEventArgs(alreadyWritten));
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
