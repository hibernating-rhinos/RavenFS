using System;
using System.ComponentModel.Composition;

namespace RavenFS.Infrastructure
{
	[MetadataAttribute]
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
	public class HandlerMetadataAttribute : ExportAttribute
	{
		public string Url { get; private set; }
		public string Method { get; private set; }

		public HandlerMetadataAttribute(string url, string method)
			: base(typeof(AbstractAsyncHandler))
		{
			Url = url;
			Method = method;
		}
	}
}