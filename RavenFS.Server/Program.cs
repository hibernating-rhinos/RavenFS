using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using NDesk.Options;

namespace RavenFS.Server
{
	using System.Configuration.Install;
	using System.IO;
	using System.Reflection;
	using System.Security.Principal;
	using System.Xml;
	using Config;
	using NLog.Config;
	using Util;

	class Program
	{
		static void Main(string[] args)
		{
			HttpEndpointRegistration.RegisterHttpEndpointTarget();
			if (RunningInInteractiveMode())
			{
				try
				{
					InteractiveRun(args);
				}
				catch (ReflectionTypeLoadException e)
				{
					EmitWarningInRed();

					Console.WriteLine(e);
					foreach (var loaderException in e.LoaderExceptions)
					{
						Console.WriteLine("- - - -");
						Console.WriteLine(loaderException);
					}

					WaitForUserInputAndExitWithError();
				}
				catch (Exception e)
				{
					EmitWarningInRed();

					Console.WriteLine(e);

					WaitForUserInputAndExitWithError();
				}
			}
			else
			{
				// no try catch here, we want the exception to be logged by Windows
				var hostingService = new HostingService
				{
					Configuration = new RavenFileSystemConfiguration()
				};

				ServiceBase.Run(hostingService);
			}
		}

		private static bool RunningInInteractiveMode()
		{
			return Environment.UserInteractive;
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
Distributed Synchronized File System for the .Net Platform
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

		private static void InteractiveRun(string[] args)
		{
			Action actionToTake = null;
			bool launchBrowser = false;
			var ravenConfiguration = new RavenFileSystemConfiguration();

			OptionSet optionSet = null;
			optionSet = new OptionSet
			{
				{"port=", port => ravenConfiguration.Port = int.Parse(port)},
				{"path=", path => ravenConfiguration.DataDirectory = path},
				//{"set={==}", "The configuration {0:option} to set to the specified {1:value}" , (key, value) =>
				//{
				//	ravenConfiguration.Settings[key] = value;
				//	ravenConfiguration.Initialize();
				//}},
				{"install", "Installs the RavenFS service", key => actionToTake= () => AdminRequired(InstallAndStart, key)},
				//{"service-name=", "The {0:service name} to use when installing or uninstalling the service, default to RavenFS", name => ProjectInstaller.SERVICE_NAME = name},
				{"uninstall", "Uninstalls the RavenFS service", key => actionToTake= () => AdminRequired(EnsureStoppedAndUninstall, key)},
				{"start", "Starts the RavenFS service", key => actionToTake= () => AdminRequired(StartService, key)},
				{"restart", "Restarts the RavenFS service", key => actionToTake= () => AdminRequired(RestartService, key)},
				{"stop", "Stops the RavenFS service", key => actionToTake= () => AdminRequired(StopService, key)},
				{"debug", "Runs RavenDB in debug mode", key => actionToTake = () => RunInDebugMode(ravenConfiguration, launchBrowser)},
				{"browser|launchbrowser", "After the server starts, launches the browser", key => launchBrowser = true},
				{"help", "Help about the command line interface", key =>
				{
					actionToTake = () => PrintUsage(optionSet);
				}},
			};


			try
			{
				if (args.Length == 0) // we default to executing in debug mode 
					args = new[] { "--debug" };

				optionSet.Parse(args);
			}
			catch (Exception e)
			{
				Console.WriteLine(e.Message);
				PrintUsage(optionSet);
				return;
			}

			if (actionToTake == null)
				actionToTake = () => RunInDebugMode(ravenConfiguration, launchBrowser);

			actionToTake();
		}

		private static void RunInDebugMode(RavenFileSystemConfiguration ravenConfiguration, bool launchBrowser)
		{
			ConfigureLogging();

			NonAdminHttp.EnsureCanListenToWhenInNonAdminContext(ravenConfiguration.Port);
			
			var sp = Stopwatch.StartNew();

			var hostingService = new HostingService
			{
				Configuration = ravenConfiguration
			};

			hostingService.Start();

			Console.WriteLine("Raven FS is ready to process requests.");
			Console.WriteLine("\tServer started in {0:#,#;;0} ms", sp.ElapsedMilliseconds);
			Console.WriteLine("\tData directory: {0}", ravenConfiguration.DataDirectory);
			Console.WriteLine("\tServer Url: {0}", ravenConfiguration.ServerUrl);

			if (launchBrowser)
			{
				try
				{
					Process.Start(ravenConfiguration.ServerUrl);
				}
				catch (Exception e)
				{
					Console.WriteLine("Could not start browser: " + e.Message);
				}
			}

			InteractiveRun();

			hostingService.Stop();
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

		private static void AdminRequired(Action actionThatMayRequiresAdminPrivileges, string cmdLine)
		{
			var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
			if (principal.IsInRole(WindowsBuiltInRole.Administrator) == false)
			{
				if (RunAgainAsAdmin(cmdLine))
					return;
			}
			actionThatMayRequiresAdminPrivileges();
		}

		private static bool RunAgainAsAdmin(string cmdLine)
		{
			try
			{
				var process = Process.Start(new ProcessStartInfo
				{
					Arguments = "--" + cmdLine,
					FileName = Assembly.GetExecutingAssembly().Location,
					Verb = "runas",
				});
				process.WaitForExit();
				return true;
			}
			catch (Exception)
			{
				return false;
			}
		}

		private static void InstallAndStart()
		{
			if (ServiceIsInstalled())
			{
				Console.WriteLine("Service is already installed");
			}
			else
			{
				ManagedInstallerClass.InstallHelper(new[] { Assembly.GetExecutingAssembly().Location });
				SetRecoveryOptions(ProjectInstaller.SERVICE_NAME);
				var startController = new ServiceController(ProjectInstaller.SERVICE_NAME);
				startController.Start();
			}
		}

		private static bool ServiceIsInstalled()
		{
			return (ServiceController.GetServices().Count(s => s.ServiceName == ProjectInstaller.SERVICE_NAME) > 0);
		}

		private static void EnsureStoppedAndUninstall()
		{
			if (ServiceIsInstalled() == false)
			{
				Console.WriteLine("Service is not installed");
			}
			else
			{
				var stopController = new ServiceController(ProjectInstaller.SERVICE_NAME);

				if (stopController.Status == ServiceControllerStatus.Running)
					stopController.Stop();

				ManagedInstallerClass.InstallHelper(new[] { "/u", Assembly.GetExecutingAssembly().Location });
			}
		}

		private static void StopService()
		{
			var stopController = new ServiceController(ProjectInstaller.SERVICE_NAME);

			if (stopController.Status == ServiceControllerStatus.Running)
			{
				stopController.Stop();
				stopController.WaitForStatus(ServiceControllerStatus.Stopped);
			}
		}


		private static void StartService()
		{
			var stopController = new ServiceController(ProjectInstaller.SERVICE_NAME);

			if (stopController.Status != ServiceControllerStatus.Running)
			{
				stopController.Start();
				stopController.WaitForStatus(ServiceControllerStatus.Running);
			}
		}

		private static void RestartService()
		{
			var stopController = new ServiceController(ProjectInstaller.SERVICE_NAME);

			if (stopController.Status == ServiceControllerStatus.Running)
			{
				stopController.Stop();
				stopController.WaitForStatus(ServiceControllerStatus.Stopped);
			}
			if (stopController.Status != ServiceControllerStatus.Running)
			{
				stopController.Start();
				stopController.WaitForStatus(ServiceControllerStatus.Running);
			}
		}

		static void SetRecoveryOptions(string serviceName)
		{
			int exitCode;
			var arguments = string.Format("failure {0} reset= 500 actions= restart/60000", serviceName);
			using (var process = new Process())
			{
				var startInfo = process.StartInfo;
				startInfo.FileName = "sc";
				startInfo.WindowStyle = ProcessWindowStyle.Hidden;

				// tell Windows that the service should restart if it fails
				startInfo.Arguments = arguments;

				process.Start();
				process.WaitForExit();

				exitCode = process.ExitCode;

				process.Close();
			}

			if (exitCode != 0)
				throw new InvalidOperationException(
					"Failed to set the service recovery policy. Command: " + Environment.NewLine + "sc " + arguments + Environment.NewLine + "Exit code: " + exitCode);
		}

		private static void WaitForUserInputAndExitWithError()
		{
			Console.WriteLine("Press any key to continue...");
			try
			{
				Console.ReadKey(true);
			}
			catch
			{
				// cannot read key?
			}
			Environment.Exit(-1);
		}

		private static void EmitWarningInRed()
		{
			var old = Console.ForegroundColor;
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine("A critical error occurred while starting the server. Please see the exception details bellow for more details:");
			Console.ForegroundColor = old;
		}
	}
}
