namespace RavenFS.Storage
{
	public class PageRange
	{
		public PageInformation Start { get; set; }

		public PageInformation End { get; set; }

		public ulong StartByte { get; set; }

		public ulong EndByte { get; set; }

		public bool IsOverlaping(PageRange pageRange)
		{
			return pageRange.Start.Id <= Start.Id;
		}

		public void Add(PageRange pageRange)
		{
			End = pageRange.End;
		}
	}
}