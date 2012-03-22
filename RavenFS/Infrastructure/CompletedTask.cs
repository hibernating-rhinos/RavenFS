using System.Threading.Tasks;

namespace RavenFS.Infrastructure
{
	public class CompletedTask
	{
		public static implicit operator Task(CompletedTask _)
		{
			var tcs = new TaskCompletionSource<object>();
			tcs.SetResult(null);
			return tcs.Task;
		}
	}

	public class CompletedTask<T>
	{
		private readonly T val;

		public CompletedTask(T val)
		{
			this.val = val;
		}

		public static implicit operator Task(CompletedTask<T> _)
		{
			var tcs = new TaskCompletionSource<T>();
			tcs.SetResult(_.val);
			return tcs.Task;
		}
	}
}