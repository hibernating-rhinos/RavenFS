using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RavenFS.Client;

namespace Tryouts
{
	class Program
	{
		static void Main(string[] args)
		{
			//var fs = new RavenFileSystemClient("http://storage.wolfeautomation.com/");
			// var fs = new RavenFileSystemClient("http://localfs/");
			var fs = new RavenFileSystemClient("http://reduction:9090/");

			SimpleTest(fs);

			// UploadFiles(fs);
			//DownloadFiles(fs);
		}

		static void SimpleTest(RavenFileSystemClient fs)
		{
			var ms = new MemoryStream();
			var streamWriter = new StreamWriter(ms);
			var expected = new string('a', 1024);
			streamWriter.Write(expected);
			streamWriter.Flush();
			ms.Position = 0;
			fs.UploadAsync("abc.txt", ms).Wait();

			var ms2 = new MemoryStream();
			fs.DownloadAsync("abc.txt", ms2).Wait();

			ms2.Position = 0;

			var actual = new StreamReader(ms2).ReadToEnd();
			Console.WriteLine(actual.Length);
		}

		static void UploadFiles(RavenFileSystemClient client)
		{
			var files = Directory.GetFiles(@"C:\@DemoData");

			Console.WriteLine("Uploading {0} files", files.Length);

			foreach (var f in files)
			{
				Console.WriteLine();

				var filename = Path.GetFileName(f);

				var fileInfo = new System.IO.FileInfo(f);

				try
				{
					var sw = new Stopwatch();
					sw.Start();

					Console.WriteLine("** Uploading: {0}, Size: {1} KB", filename, (fileInfo.Length / 1024));
					client.UploadAsync(filename, new NameValueCollection(), File.OpenRead(f)).Wait();

					sw.Stop();
					Console.WriteLine("** Done: {0}: Seconds {1}", filename,
									  (sw.ElapsedMilliseconds / 1000));
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex.InnerException);
				}
			}
		}

		static void DownloadFiles(RavenFileSystemClient client)
		{
			var files = Directory.GetFiles(@"C:\@DemoData");

			Console.WriteLine("Downloading {0} files", files.Length);

			foreach (var f in files)
			{
				Console.WriteLine();

				var filename = Path.GetFileName(f);

				try
				{
					var sw = new Stopwatch();
					sw.Start();

					var stream = File.Create(Path.Combine(@"C:\@DemoFiles", filename));

					Console.WriteLine("** Downloading: {0}", filename);
					client.DownloadAsync(filename, stream).Wait();

					stream.Close();
					stream.Dispose();

					sw.Stop();
					Console.WriteLine("** Done: {0}: Seconds {1}", filename,
									  (sw.ElapsedMilliseconds / 1000));
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex.InnerException);
				}
			}
		}
	}
}
