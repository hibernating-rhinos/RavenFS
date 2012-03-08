namespace RavenFS.Controllers
{
	public class StatsController : RavenController
	{
		public object Get()
		{
			var count = 0;
			Storage.Batch(accessor =>
			{
				count = accessor.GetFileCount();
			});
			return new Stats
			{
				FileCount = count
			};
		}

		public class Stats
		{
			public int FileCount;
		}
	}
}