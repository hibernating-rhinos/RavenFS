using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;


namespace RavenFS.Client
{
	public class NameValueCollection : IEnumerable<string>
	{
        private readonly Dictionary<string,string> keyValues = new Dictionary<string, string>();

        public IEnumerable<string> AllKeys { get { return keyValues.Keys; } }

		public NameValueCollection()
		{
			
		}

		public NameValueCollection(WebHeaderCollection headers)
		{
			foreach (string header in headers)
			{
				keyValues[header] = headers[header];
			}
		}

		public string[] GetValues(string key)
		{
			string value;
			if (keyValues.TryGetValue(key, out value))
				return new[] {value};
			return new string[0];
		}

		public string Get(string key)
		{
			string value;
            keyValues.TryGetValue(key, out value);
			return value;
		}

	    public IEnumerator<string> GetEnumerator()
	    {
	        return AllKeys.GetEnumerator();
	    }

	    IEnumerator IEnumerable.GetEnumerator()
	    {
	        return GetEnumerator();
	    }

	    public void Add(string key, string value)
	    {
	        keyValues.Add(key, value);
	    }

	    public string this[string key]
	    {
            get { return Get(key); }
            set { keyValues[key] = value; }
	    }
	}
}