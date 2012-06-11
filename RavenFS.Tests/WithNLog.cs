namespace RavenFS.Tests
{
	using System.Xml;
	using NLog.Config;
	using Util;

	public class WithNLog
	{
		static WithNLog()
		{
			if (NLog.LogManager.Configuration != null)
				return;

			HttpEndpointRegistration.RegisterHttpEndpointTarget();

			using (var stream = typeof(WithNLog).Assembly.GetManifestResourceStream("RavenFS.Tests.DefaultLogging.config"))
			using (var reader = XmlReader.Create(stream))
			{
				NLog.LogManager.Configuration = new XmlLoggingConfiguration(reader, "default-config");
			}
		}
	}
}