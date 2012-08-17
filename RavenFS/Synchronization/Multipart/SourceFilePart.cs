namespace RavenFS.Synchronization.Multipart
{
	using System.IO;
	using System.Net.Http;
	using System.Net.Http.Headers;
	using System.Threading.Tasks;
	using RavenFS.Util;
	using Storage;

	public class SourceFilePart : StreamContent
	{
		private readonly NarrowedStream sourceChunk;
		private readonly PageRange pageRange;

		public SourceFilePart(NarrowedStream sourceChunk, PageRange pageRange)
			: base(sourceChunk)
		{
			this.sourceChunk = sourceChunk;
			this.pageRange = pageRange;

			Headers.ContentDisposition = new ContentDispositionHeaderValue("file");
			Headers.ContentDisposition.Parameters.Add(new NameValueHeaderValue(SyncingMultipartConstants.NeedType, SyncingNeedType));
			Headers.ContentDisposition.Parameters.Add(new NameValueHeaderValue(SyncingMultipartConstants.RangeFrom, pageRange.StartByte.ToString()));
			Headers.ContentDisposition.Parameters.Add(new NameValueHeaderValue(SyncingMultipartConstants.RangeTo, pageRange.EndByte.ToString()));
			Headers.ContentDisposition.Parameters.Add(new NameValueHeaderValue(SyncingMultipartConstants.PageRangeFrom, pageRange.Start.Id.ToString()));
			Headers.ContentDisposition.Parameters.Add(new NameValueHeaderValue(SyncingMultipartConstants.PageRangeTo, pageRange.End.Id.ToString()));

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