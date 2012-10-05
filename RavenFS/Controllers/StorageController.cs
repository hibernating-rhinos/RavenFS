namespace RavenFS.Controllers
{
	using System.Threading.Tasks;
	using System.Web.Http;

	public class StorageController : RavenController
	{
		[AcceptVerbs("POST")]
		public Task CleanUp()
		{
			return StorageOperationsTask.CleanupDeletedFilesAsync();
		}
	}
}