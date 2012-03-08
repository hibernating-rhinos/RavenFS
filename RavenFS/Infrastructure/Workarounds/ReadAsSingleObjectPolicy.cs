using System.Web.Http.Controllers;
using System.Web.Http.ModelBinding;

namespace RavenFS.Infrastructure.Workarounds
{
	public class ReadAsSingleObjectPolicy : IRequestContentReadPolicy
	{
		public RequestContentReadKind GetRequestContentReadKind(HttpActionDescriptor actionDescriptor)
		{
			switch (actionDescriptor.ActionName)
			{
				case "Post":
				case "Put":
					return RequestContentReadKind.None;
				default:
					return RequestContentReadKind.AsKeyValuePairs;
			}
		}
	}
}