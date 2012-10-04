namespace RavenFS.Config
{
	using System;
	using System.Collections.Generic;
	using System.Configuration;
	using System.Linq;

	public class RavenFileSystemConfiguration : InMemoryConfiguration
	{
		public RavenFileSystemConfiguration()
		{
			LoadConfigurationAndInitialize(ConfigurationManager.AppSettings.AllKeys.Select(k => Tuple.Create(k, ConfigurationManager.AppSettings[k])));
		}

		private void LoadConfigurationAndInitialize(IEnumerable<Tuple<string, string>> values)
		{
			foreach (var setting in values)
			{
				if (setting.Item1.StartsWith("Raven/", StringComparison.InvariantCultureIgnoreCase))
					Settings[setting.Item1] = setting.Item2;
			}

			Initialize();
		}

		public void LoadFrom(string path)
		{
			var configuration = ConfigurationManager.OpenExeConfiguration(path);
			LoadConfigurationAndInitialize(configuration.AppSettings.Settings.AllKeys.Select(k => Tuple.Create(k, ConfigurationManager.AppSettings[k])));
		}
	}
}