using System;
using System.Collections.Specialized;
using System.IO;
using System.Web;
using RavenFS.Client;

namespace Tryouts
{
	public class RavenUploaderHandler: IHttpHandler
	{
		RavenFileSystemClient client = new RavenFileSystemClient("http://localhost/ravenfs");

		public void ProcessRequest(HttpContext context)
		{
			var parser = new MultiPartParser(context.Request.GetBufferlessInputStream());
			Tuple<Stream, NameValueCollection> tuple;
			while((tuple = parser.Next()) != null)
			{
				var upload = client.UploadAsync(tuple.Item2["Content-Disposition"], tuple.Item2, tuple.Item1);
				upload.Wait();// we have to wait here because we are reading from a single stream
			}
		}

		public bool IsReusable
		{
			get { return true; }
		}
	}
}