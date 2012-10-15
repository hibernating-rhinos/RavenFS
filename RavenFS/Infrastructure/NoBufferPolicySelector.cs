namespace RavenFS.Infrastructure
{
	using System.Web.Http.WebHost;

	public class NoBufferPolicySelector : WebHostBufferPolicySelector
	{
		public override bool UseBufferedInputStream(object hostContext)
		{
			return false;
		}

		public override bool UseBufferedOutputStream(System.Net.Http.HttpResponseMessage response)
		{
			return false;
		}
	}
}