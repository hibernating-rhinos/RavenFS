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

			using(var f = File.OpenRead(@"C:\Users\Ayende\Downloads\Rhino.ServiceBus.dll"))
			{
				fs.UploadAsync("Rhino.ServiceBus.dll", f).Wait();
			}
		}
		
	}
}
