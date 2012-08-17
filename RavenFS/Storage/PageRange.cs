namespace RavenFS.Storage
{
	public class PageRange
	{
		public PageInformation Start { get; set; }

		public PageInformation End { get; set; }

		public long StartByte { get; set; }

		public long EndByte { get; set; }

		public bool IsOverlaping(PageRange pageRange)
		{
			return pageRange.Start.Id <= Start.Id;
		}

		public void Add(PageRange pageRange)
		{
			End = pageRange.End;
			EndByte = pageRange.EndByte;
		}
	}
}