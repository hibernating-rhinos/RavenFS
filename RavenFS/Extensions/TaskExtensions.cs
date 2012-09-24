namespace RavenFS.Extensions
{
	using System.Threading.Tasks;

	public static class TaskExtensions
	{
		public static void AssertNotFaulted(this Task task)
		{
			if (task.Status == TaskStatus.Faulted)
				task.Wait(); // throws
		}
	}
}