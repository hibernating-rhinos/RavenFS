namespace RavenFS.Storage
{
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;

	public class PageRange
	{
		public PageRange()
		{
			OrderedPages = new List<PageInformation>();
		}

		public List<PageInformation> OrderedPages { get; set; }

		public long StartByte { get; set; }

		public long EndByte { get; set; }

		public bool IsOverlaping(PageRange compared)
		{
			var start = OrderedPages.FirstOrDefault();
			var comparedStart = compared.OrderedPages.FirstOrDefault();

			var end = OrderedPages.LastOrDefault();
			var comparedEnd = compared.OrderedPages.LastOrDefault();

			if (start == null || comparedStart == null || end == null || comparedEnd == null)
			{
				throw new InvalidDataException("Page range should contain ordered list of pages");
			}

			return comparedStart.Id <= start.Id;
		}

		public void Add(PageRange pageRange)
		{
			foreach (var page in pageRange.OrderedPages.Where(page => !OrderedPages.Contains(page)))
			{
				OrderedPages.Add(page);
			}

			EndByte = pageRange.EndByte;
		}
	}
}