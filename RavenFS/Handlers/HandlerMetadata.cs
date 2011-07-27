using System;
using System.ComponentModel.Composition;

namespace RavenFS.Handlers
{
	[MetadataAttribute]
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
	public class HandlerMetadataAttribute : ExportAttribute
	{
		public string Url { get; set; }
		public string Method { get; set; }

		public HandlerMetadataAttribute(string url, string method)
			: base(typeof(AbstractAsyncHandler))
		{
			Url = url;
			Method = method;
		}
	}
}