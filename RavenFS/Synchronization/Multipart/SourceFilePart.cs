namespace RavenFS.Synchronization.Multipart
{
	using System.Globalization;
	using System.IO;
	using System.Net.Http;
	using System.Net.Http.Headers;
	using System.Threading.Tasks;
	using RavenFS.Util;

	public class SourceFilePart : StreamContent
	{
		private readonly NarrowedStream sourceChunk;

		public SourceFilePart(NarrowedStream sourceChunk)
			: base(sourceChunk)
		{
			this.sourceChunk = sourceChunk;

			Headers.ContentDisposition = new ContentDispositionHeaderValue("file");
			Headers.ContentDisposition.Parameters.Add(new NameValueHeaderValue(SyncingMultipartConstants.NeedType, SyncingNeedType));
			Headers.ContentDisposition.Parameters.Add(new NameValueHeaderValue(SyncingMultipartConstants.RangeFrom, sourceChunk.From.ToString(CultureInfo.InvariantCulture)));
			Headers.ContentDisposition.Parameters.Add(new NameValueHeaderValue(SyncingMultipartConstants.RangeTo, sourceChunk.To.ToString(CultureInfo.InvariantCulture)));

			Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
		}

		public string SyncingNeedType
		{
			get { return "source"; }
		}

		protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext context)
		{
			sourceChunk.Seek(0, SeekOrigin.Begin);
			return base.SerializeToStreamAsync(stream, context);
		}
	}
}