using System.Threading.Tasks;
using System.Web.Http;

namespace RavenFS.Controllers
{
	public class StorageController : RavenController
	{
		[AcceptVerbs("POST")]
		public Task CleanUp()
		{
			return StorageOperationsTask.CleanupDeletedFilesAsync();
		}

		[AcceptVerbs("POST")]
		public Task RetryRenaming()
		{
			return StorageOperationsTask.ResumeFileRenamingAsync();
		}
	}
}