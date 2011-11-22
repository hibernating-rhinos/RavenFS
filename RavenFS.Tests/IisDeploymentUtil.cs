using System;
using System.IO;
using Raven.Database.Extensions;

namespace RavenFS.Tests
{
	public class IisDeploymentUtil
	{
		protected const string WebDirectory = @".\RavenIISTestWeb\";

		public static string DeployWebProjectToTestDirectory()
		{
			var fullPath = Path.GetFullPath(WebDirectory);
			if (Directory.Exists(fullPath))
			{
				IOExtensions.DeleteDirectory(fullPath);
			}

			IOExtensions.CopyDirectory(GetRavenWebSource(), WebDirectory);

			IOExtensions.DeleteDirectory(Path.Combine(fullPath, "Data.ravenfs"));
			IOExtensions.DeleteDirectory(Path.Combine(fullPath, "Index.ravenfs"));

			return fullPath;
		}

		private static string GetRavenWebSource()
		{
			foreach (var path in new[] { @"../../../RavenFS" })
			{
				var fullPath = Path.GetFullPath(path);

				if (Directory.Exists(fullPath) && File.Exists(Path.Combine(fullPath, "web.config")))
				{
					var combine = Path.Combine(fullPath, "bin");
					if (!Directory.Exists(combine) || Directory.GetFiles(combine, "RavenFS.dll").Length == 0)
						throw new Exception("RavenFS\\bin at " + fullPath + " was nonexistant or empty, you need to build RavenFS.");

					return fullPath;
				}
			}

			throw new FileNotFoundException("Could not find source directory for RavenFS");
		}
	}
}