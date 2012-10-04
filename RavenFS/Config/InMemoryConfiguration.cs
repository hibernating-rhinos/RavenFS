namespace RavenFS.Config
{
	using System;
	using System.Collections.Specialized;
	using System.IO;
	using System.Web;
	using Extensions;

	public class InMemoryConfiguration
	{
		private string dataDirectory;
		private string indexStoragePath;

		public InMemoryConfiguration()
		{
			Settings = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);
		}

		public NameValueCollection Settings { get; set; }

		public void Initialize()
		{
			// Data settings
			DataDirectory = Settings["Raven/DataDir"] ?? @"~\Data";

			if (string.IsNullOrEmpty(Settings["Raven/IndexStoragePath"]) == false)
			{
				IndexStoragePath = Settings["Raven/IndexStoragePath"];
			}

			// HTTP Settings
			HostName = Settings["Raven/HostName"];

			Port = PortUtil.GetPort(Settings["Raven/Port"]);
		}

		public string DataDirectory
		{
			get { return dataDirectory; }
			set { dataDirectory = value == null ? null : value.ToFullPath(); }
		}

		public string IndexStoragePath
		{
			get
			{
				if (string.IsNullOrEmpty(indexStoragePath))
					return Path.Combine(DataDirectory, "Index.ravenfs");

				return indexStoragePath;
			}
			set
			{
				indexStoragePath = value.ToFullPath();
			}
		}

		public string ServerUrl
		{
			get
			{
				if (HttpContext.Current != null)// running in IIS, let us figure out how
				{
					var url = HttpContext.Current.Request.Url;
					return new UriBuilder(url)
					{
						Path = HttpContext.Current.Request.ApplicationPath,
						Query = ""
					}.Uri.ToString();
				}
				return new UriBuilder("http", (HostName ?? Environment.MachineName), Port).Uri.ToString();
			}
		}

		public string HostName { get; set; }

		public int Port { get; set; }
	}
}