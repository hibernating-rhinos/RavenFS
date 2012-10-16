using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RavenFS.Client
{
	using System.Threading.Tasks;

	internal class ListenableStream : Stream
    {
		private const double Kb = 1024;
		private const double Mb = Kb * 1024;
		private const double Gb = Mb * 1024;
	    private const double MbPrecision = 0.1 * Mb;
	    private const double GbPrecision = 0.01 * Gb;

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
		private long lastNotifiedRead;
		private long lastNotifiedWritten;
		private readonly long allBytesToProcess = long.MaxValue;

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

	        if (source.CanSeek)
	        {
		        allBytesToProcess = source.Length;
	        }
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

	        Task.Factory.StartNew(() =>
	        {
		        if (alreadyRead <= Mb ||
		            alreadyRead <= Gb && (alreadyRead - lastNotifiedRead) >= MbPrecision ||
		            alreadyRead > Gb && (alreadyRead - lastNotifiedRead) >= GbPrecision ||
		            (allBytesToProcess - alreadyRead < Mb))
		        {
			        InvokeReadingProgress(new ProgressEventArgs(alreadyRead));
			        lastNotifiedRead = alreadyRead;
		        }
	        });

            return result;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            source.Write(buffer, offset, count);
            alreadyWritten += count;

	        Task.Factory.StartNew(() =>
	        {
		        if (alreadyWritten <= Mb ||
		            alreadyWritten <= Gb && (alreadyWritten - lastNotifiedWritten) >= MbPrecision ||
		            alreadyWritten > Gb && (alreadyWritten - lastNotifiedWritten) >= GbPrecision ||
					(allBytesToProcess - alreadyRead < Mb))
		        {
			        InvokeWrittingProgress(new ProgressEventArgs(alreadyWritten));
			        lastNotifiedWritten = alreadyWritten;
		        }
	        });
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
