using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using RavenFS.Client;

namespace Tryouts
{
	class Program
	{
		static void Main(string[] args)
		{
			var fs = new RavenFileSystemClient("http://localhost");

			var ms = new MemoryStream();
			var streamWriter = new StreamWriter(ms);
			var expected = new string('a', 1024 * 1024 * 500);
			streamWriter.Write(expected);
			streamWriter.Flush();
			ms.Position = 0;

			Console.WriteLine("Writing...");

			try
			{
				fs.UploadAsync("large-file-100mb", new NameValueCollection(), ms, (s, written) => Console.WriteLine("{0:#,#} kb", written/1024)).Wait();
			}
			catch (AggregateException e)
			{
				while(e.InnerException is AggregateException)
				{
					e = (AggregateException) e.InnerException;
				}
				var we = e.InnerException as WebException;
				if (we == null)
					throw;
				var httpWebResponse = we.Response as HttpWebResponse;
				if (httpWebResponse == null)
					throw;

				Console.WriteLine(new StreamReader(httpWebResponse.GetResponseStream()).ReadToEnd());
			}
		}
		
	}
}
