using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Web;
using Newtonsoft.Json;

namespace RavenFS.Notifications
{
	using Client;

	public class TypeHidingJsonSerializer 
    {
        private static JsonSerializerSettings settings;

        static TypeHidingJsonSerializer()
        {
            settings = new JsonSerializerSettings()
                           {
                               Binder = new TypeHidingBinder(),
                               TypeNameHandling = TypeNameHandling.Auto,
                           };
        }

        public string Stringify(object obj)
        {
            return JsonConvert.SerializeObject(obj, Formatting.None, settings);
        }

        public object Parse(string json)
        {
            return JsonConvert.DeserializeObject(json, settings);
        }

        public object Parse(string json, Type targetType)
        {
            return JsonConvert.DeserializeObject(json, targetType, settings);
        }

        public T Parse<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json, settings);
        }
    }

    /// <summary>
    /// We don't want to pollute our API with details about the types of our notification objects, so we bind
    /// based just on the type name, and assume the rest.
    /// </summary>
    internal class TypeHidingBinder : SerializationBinder
    {
        ConcurrentDictionary<string, Type> cachedTypes = new ConcurrentDictionary<string, Type>();

        public override Type BindToType(string assemblyName, string typeName)
        {
            Type type;

            if (!cachedTypes.TryGetValue(typeName, out type))
            {
                var @namespace = typeof(Notification).Namespace;
                var fullTypeName = @namespace + "." + typeName;
                type = Type.GetType(fullTypeName);

                cachedTypes.TryAdd(typeName, type);
            }

            return type;
        }

        public override void BindToName(Type serializedType, out string assemblyName, out string typeName)
        {
            assemblyName = null;
            typeName = serializedType.Name;
        }
    }
}