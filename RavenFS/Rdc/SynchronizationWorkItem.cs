namespace RavenFS.Rdc
{
	using System.Threading.Tasks;
	using Client;

	public abstract class SynchronizationWorkItem
	{
		protected SynchronizationWorkItem(string fileName)
		{
			FileName = fileName;
		}

		public string FileName { get; set; }

		public abstract Task<SynchronizationReport> Perform(string destination);
	}
}