using System.Threading.Tasks;

namespace RavenFS.Infrastructure
{
	public static class TaskExtensions
	{
		public static void AssertNotFaulted(this Task task)
		{
			if (task.Status == TaskStatus.Faulted)
				task.Wait(); // throws
		}
	}
}