using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace RavenFS.Infrastructure
{
	public class HandlerMetadata
	{
		public HandlerMetadata(IDictionary<string, object> args)
		{
			Url = new Regex((string) args["Url"], RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
			Method = (string) args["Method"];
		}

		public Regex Url { get; set; }
		public string Method { get; set; }

		public bool Matches(string requestType, string url)
		{
			return Method == requestType && Url.IsMatch(url);
		}
	}
}