﻿namespace RavenFS.Config
{
	using System;
	using System.Globalization;
	using System.IO;
	using System.Text;
	using System.Xml;
	using NLog;

	public class ServerUrlUtil
	{
		private static readonly Logger logger = LogManager.GetCurrentClassLogger();

		public static bool TrySaveServerUrlForNextTime(string serverUrl)
		{
			try
			{
				using (var localConfig = File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "local.config")))
				using (var writer = XmlWriter.Create(localConfig, new XmlWriterSettings
				{
					Indent = true,
					Encoding = Encoding.UTF8
				}))
				{
					writer.WriteStartElement("LocalConfig");
					writer.WriteAttributeString("ServerUrl", serverUrl.ToString(CultureInfo.InvariantCulture));
					writer.WriteEndElement();
				}

				return true;
			}
			catch (Exception e)
			{
				logger.InfoException("Could not store server url", e);
			}

			return false;
		}

		public static bool TryReadPreviouslySavedServerUrl(out string serverUrl)
		{
			serverUrl = string.Empty;
			string localConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "local.config");
			if (File.Exists(localConfigPath) == false)
			{
				return false;
			}
			var doc = new XmlDocument();
			doc.Load(localConfigPath);
			if (doc.DocumentElement == null)
				return false;

			serverUrl = doc.DocumentElement.GetAttribute("ServerUrl");
			
			if (!Uri.IsWellFormedUriString(serverUrl, UriKind.Absolute))
				return false;

			return true;
		} 
	}
}