using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Web;
using RavenFS.Storage;

namespace RavenFS.Extensions
{
    public static class ConfigurationExtension
    {
        public static string GetConfigurationValue(this StorageActionsAccessor accessor, string key)
        {
            return accessor.GetConfig(key)["value"];
        }

        public static void SetConfigurationValue(this StorageActionsAccessor accessor, string key, string value)
        {
            accessor.SetConfig(key, new NameValueCollection { { "value", value } });
        }
    }
}