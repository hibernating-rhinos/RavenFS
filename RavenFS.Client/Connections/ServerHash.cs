using System;
using System.Text;
using System.Security.Cryptography;
#if SILVERLIGHT || NETFX_CORE
using RavenFS.Client.Silverlight.MissingFromSilverlight;
#else

#endif

namespace RavenFS.Client.Connections
{
	public static class ServerHash
	{
		public static string GetServerHash(string url)
		{
			var bytes = Encoding.UTF8.GetBytes(url);
			return BitConverter.ToString(GetHash(bytes));
		}

		private static byte[] GetHash(byte[] bytes)
		{
#if SILVERLIGHT || NETFX_CORE
			return MD5Core.GetHash(bytes);
#else
			using (var md5 = MD5.Create())
			{
				return md5.ComputeHash(bytes);
			}
#endif
		}
	}
}