using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using RavenFS.Storage;

namespace RavenFS.Extensions
{
    public static class ConfigurationExtension
    {
        public static T GetConfigurationValue<T>(this StorageActionsAccessor accessor, string key)
        {
            var value = accessor.GetConfig(key)["value"];
            return new JsonSerializer().Deserialize<T>(new JsonTextReader(new StringReader(value)));
        }

        public static bool TryGetConfigurationValue<T>(this StorageActionsAccessor accessor, string key, out T result)
        {
            try
            {
                result = GetConfigurationValue<T>(accessor, key);
                return true;
            } 
            catch(FileNotFoundException)
            {
                result = default(T);
                return false;
            }
        }

        public static void SetConfigurationValue<T>(this StorageActionsAccessor accessor, string key, T objectToSave)
        {
            var sb = new StringBuilder();
            var jw = new JsonTextWriter(new StringWriter(sb));
            new JsonSerializer().Serialize(jw, objectToSave);
            var value = sb.ToString();
            accessor.SetConfig(key, new NameValueCollection { { "value", value } });
        }

        public static IEnumerable<string> GetConfigNames(this StorageActionsAccessor accessor)
        {
            const int pageSize = 20;
            var start = 0;
        	int old;
        	do
        	{
        		old = start;
        		var items = accessor.GetConfigNames(start, pageSize);
        		foreach (var item in items)
        		{
        			start++;
        			yield return item;
        		}

        	} while (old == start);
        }

		public static IList<T> GetConfigsStartWithPrefix<T>(this StorageActionsAccessor accessor, string prefix, int start, int take)
		{
			var configs = accessor.GetConfigsStartWithPrefix(prefix, start, take);

			return configs.Select(config => new JsonSerializer().Deserialize<T>(new JsonTextReader(new StringReader(config["value"])))).ToList();
		} 
    }
}