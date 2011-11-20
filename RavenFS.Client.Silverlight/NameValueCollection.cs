using System.Collections.Generic;
using System.Net;

namespace RavenFS.Client
{
	public class NameValueCollection : Dictionary<string,string>
	{
		public IEnumerable<string> AllKeys { get { return Keys; } }

		public NameValueCollection()
		{
			
		}

		public NameValueCollection(WebHeaderCollection headers)
		{
			foreach (string header in headers)
			{
				this[header] = headers[header];
			}
		}

		public string[] GetValues(string key)
		{
			string value;
			if (TryGetValue(key, out value))
				return new[] {value};
			return new string[0];
		}

		public string Get(string key)
		{
			string value;
			TryGetValue(key, out value);
			return value;
		}
	}
}