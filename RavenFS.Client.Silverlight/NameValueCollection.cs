using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;


namespace RavenFS.Client
{
	public class NameValueCollection : IEnumerable<string>
	{
        private readonly Dictionary<string, IList<string>> keyValues = new Dictionary<string, IList<string>>();

        public IEnumerable<string> AllKeys { get { return keyValues.Keys; } }

		public NameValueCollection()
		{
			
		}

		public NameValueCollection(WebHeaderCollection headers)
		{
			foreach (string header in headers)
			{
				Add(header, headers[header]);
			}
		}

		public string[] GetValues(string key)
		{
			IList<string> value;
			if (keyValues.TryGetValue(key, out value))
				return value.ToArray();
			return new string[0];
		}

		public string Get(string key)
		{
            IList<string> values;
            keyValues.TryGetValue(key, out values);
			return string.Join(",", values);
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
            IList<string> values;
            if (!keyValues.TryGetValue(key, out values))
            {
                values = new List<string>();
                keyValues.Add(key, values);
            }
            values.Add(value);
	    }

	    public string this[string key]
	    {
            get { return Get(key); }
            set { keyValues[key] = new List<string>() { value }; }
	    }

	    public void Remove(string key)
	    {
	        keyValues.Remove(key);
	    }
	}
}