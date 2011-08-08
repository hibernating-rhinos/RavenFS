using System;
using System.IO;
using System.Net;
using Microsoft.VisualStudio.WebHost;
using Raven.Database.Extensions;

namespace RavenFS.Tests
{
	public abstract class ServerTest : IDisposable
	{
		private readonly Server server;

		static ServerTest()
		{
			try
			{
				new Uri("http://localhost/?query=Customer:Northwind%20AND%20Preferred:True");
			}
			catch (Exception)
			{
			}
		}

		protected WebClient webClient = new WebClient
		{
			BaseAddress = "http://localhost:9090"
		};

		protected ServerTest()
		{
			var physicalPath = Path.GetFullPath("../../../RavenFS");

			IOExtensions.DeleteDirectory("TestDB");

			Directory.CreateDirectory("TestDB");
			Directory.CreateDirectory("TestDB/bin");

			foreach (var file in Directory.GetFiles(Path.Combine(physicalPath, "bin")))
			{
				File.Copy(file, Path.Combine("TestDB", "bin", Path.GetFileName(file)));
			}

			File.Copy(Path.Combine(physicalPath, "web.config"), Path.Combine("TestDB", "web.config"));

			server = new Server(9090, "/", Path.GetFullPath("TestDB"));
			server.Start();
		}

		protected HttpWebRequest CreateWebRequest(string url)
		{
			return (HttpWebRequest) WebRequest.Create("http://localhost:9090" + url);
		}

		public void Dispose()
		{
			server.Stop();
		}
	}
}