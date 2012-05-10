namespace RavenFS.Rdc
{
	using System.Collections.Concurrent;
	using System.Threading;

	public class SynchronizationQueue
	{
		internal class IntHolder
		{
			public int Value;
		}

		private readonly ConcurrentDictionary<string, IntHolder> activeSynchronizationTasks = new ConcurrentDictionary<string, IntHolder>();

		public int NumberOfActiveSynchronizationTasksFor(string destination)
		{
			var holder = activeSynchronizationTasks.GetOrAdd(destination, new IntHolder());
			
			return Thread.VolatileRead(ref holder.Value);
		}

		public void Add(string destination)
		{
			var holder = activeSynchronizationTasks.GetOrAdd(destination, new IntHolder());
			Thread.VolatileWrite(ref holder.Value, holder.Value + 1);
		}

		public void Remove(string destination)
		{
			var holder = activeSynchronizationTasks.GetOrAdd(destination, new IntHolder());
			Thread.VolatileWrite(ref holder.Value, holder.Value - 1);
		}
	}
}