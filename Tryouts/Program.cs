using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using RavenFS.Client;
using System.Threading.Tasks;

namespace Tryouts
{
	class Program
	{
		static void Main(string[] args)
		{
			Put();
			//Get();
		}

		private static void Put()
		{
			var file = File.OpenRead(@"C:\Users\Ayende\Downloads\2011-08-02_12-11-56.avi");

			var sp = Stopwatch.StartNew();

			var request = (HttpWebRequest)WebRequest.Create("http://localhost:37229/files/2011-08-02_12-11-56.avi");
			request.Method = "PUT";

			using (var s = request.GetRequestStream())
			{
				file.CopyTo(s);
			}
			request.GetResponse().Close();


			file.Dispose();

			Console.WriteLine(sp.ElapsedMilliseconds);
		}

		private static void Get()
		{
			var file = File.OpenWrite(@"C:\Users\Ayende\Downloads\2011-08-02_12-11-56.avi.copy");

			var sp = Stopwatch.StartNew();

			var request = (HttpWebRequest)WebRequest.Create("http://localhost:37229/files/2011-08-02_12-11-56.avi");

			using (var a = request.GetResponse())
			{
				a.GetResponseStream().CopyTo(file);
			}


			file.Dispose();

			Console.WriteLine(sp.ElapsedMilliseconds);
		}
	}
}
