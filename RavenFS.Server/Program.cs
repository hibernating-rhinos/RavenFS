using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using NDesk.Options;
using RavenFS.Extensions;

namespace RavenFS.Server
{
	using System.IO;
	using System.Xml;
	using Config;
	using NLog.Config;
	using Util;

	class Program
	{
		static void Main(string[] args)
		{
			HttpEndpointRegistration.RegisterHttpEndpointTarget();

			var options = new RavenFileSystemConfiguration();

			var programOptions = new OptionSet
			{
				{"port=", port => options.Port = int.Parse(port)},
				{"path=", path => options.DataDirectory = path}
			};

			try
			{
				if (args.Length == 0) // we default to executing in debug mode 
					args = new[] { "--debug" };

				programOptions.Parse(args);
			}
			catch (Exception e)
			{
				Console.WriteLine(e.Message);
				PrintUsage(programOptions);
				return;
			}


			Console.WriteLine("Raven FS is ready to process requests.");
			Console.WriteLine("\tData directory: {0}", options.DataDirectory);
			Console.WriteLine("\tServer Url: {0}", options.ServerUrl);

			var hostingService = new HostingService
			{
				Configuration = options
			};

			ConfigureLogging();

			if(Environment.UserInteractive)
			{
				hostingService.Start();
				InteractiveRun();
				hostingService.Stop();
			}
			else
			{
				ServiceBase.Run(hostingService);
			}
		}

		private static void InteractiveRun()
		{
			bool done = false;
			var actions = new Dictionary<string, Action>
			{
				{"cls", () => Console.Clear()},
				{
					"gc", () =>
					{
						long before = Process.GetCurrentProcess().WorkingSet64;
						Console.WriteLine("Starting garbage collection, current memory is: {0:#,#.##;;0} MB", before/1024d/1024d);
						GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
						var after = Process.GetCurrentProcess().WorkingSet64;
						Console.WriteLine("Done garbage collection, current memory is: {0:#,#.##;;0} MB, saved: {1:#,#.##;;0} MB",
						                  after/1024d/1024d,
						                  (before - after)/1024d/1024d);
					}
				},
				{
					"q", () => done = true
				}
			};

			WriteInteractiveOptions(actions);
			while (done == false)
			{
				var readLine = Console.ReadLine() ?? "";

				Action value;
				if (actions.TryGetValue(readLine, out value) == false)
				{
					Console.WriteLine("Could not understand: {0}", readLine);
					WriteInteractiveOptions(actions);
					continue;
				}

				value();
			}
		}

		private static void WriteInteractiveOptions(Dictionary<string, Action> actions)
		{
			Console.WriteLine("Available commands: {0}", string.Join(", ", actions.Select(x => x.Key)));
		}

		private static void PrintUsage(OptionSet optionSet)
		{
			Console.WriteLine(
				@"
Raven File System.
Distributed Replicated File System for the .Net Platform
----------------------------------------
Copyright (C) 2008 - {0} - Hibernating Rhinos
----------------------------------------
Command line ptions:",
				DateTime.Now.Year);

			optionSet.WriteOptionDescriptions(Console.Out);

			Console.WriteLine(@"
Enjoy...
");
		}

		private static void ConfigureLogging()
		{
			var nlogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NLog.config");
			if (File.Exists(nlogPath))
				return;// that overrides the default config

			using (var stream = typeof(Program).Assembly.GetManifestResourceStream("RavenFS.Server.DefaultLogging.config"))
			using (var reader = XmlReader.Create(stream))
			{
				NLog.LogManager.Configuration = new XmlLoggingConfiguration(reader, "default-config");
			}
		}
	}
}
