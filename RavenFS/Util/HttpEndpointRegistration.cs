using System;
using NLog.Config;

namespace RavenFS.Util
{
	public class HttpEndpointRegistration
	{
		public static void RegisterHttpEndpointTarget()
		{
			Type type;
			if (ConfigurationItemFactory.Default.Targets.TryGetDefinition("HttpEndpoint", out type) == false)
				ConfigurationItemFactory.Default.Targets.RegisterDefinition("HttpEndpoint", typeof (BoundedMemoryTarget));
		}
	}
}