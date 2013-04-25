using System.Collections.Generic;

namespace RavenFS.Studio.Extensions
{
    public static class IDictionaryExtensions
    {
        public static TValue GetValueOrDefault<TKey,TValue>(this IDictionary<TKey,TValue> dictionary, TKey key, TValue @default)
        {
            TValue value;
            return dictionary.TryGetValue(key, out value) ? value : @default;
        }
    }
}
