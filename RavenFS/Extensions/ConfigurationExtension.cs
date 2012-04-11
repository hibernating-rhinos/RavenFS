using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using Newtonsoft.Json;
using RavenFS.Storage;

namespace RavenFS.Extensions
{
    public static class ConfigurationExtension
    {
        // TODO replace my json serializer to this one and move that class to a more common namespace
        public static T GetConfigurationValue<T>(this StorageActionsAccessor accessor, string key)
        {
            var value = accessor.GetConfig(key)["value"];
            return new JsonSerializer().Deserialize<T>(new JsonTextReader(new StringReader(value)));
        }

        public static bool TryGetConfigurationValue<T>(this StorageActionsAccessor accessor, string key, ref T result)
        {
            try
            {
                result = GetConfigurationValue<T>(accessor, key);
                return true;
            } 
            catch(FileNotFoundException)
            {
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
    }
}