using System;
using System.Collections.Generic;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using RavenFS.Client;
using RavenFS.Studio.Models;

namespace RavenFS.Studio.Extensions
{
    public static class IEnumerableExtensions
    {
        public static IEnumerable<T> Apply<T>(this IEnumerable<T> enumerable, Func<IEnumerable<T>, IEnumerable<T>> action)
        {
            return action(enumerable);
        } 

        public static NameValueCollection ToNameValueCollection(this IEnumerable<EditableKeyValue> items)
        {
            var collection = new NameValueCollection();

            foreach (var item in items)
            {
                collection.Add(item.Key, item.Value);
            }

            return collection;
        }

        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> items)
        {
            return new HashSet<T>(items);
        } 

        public static IEnumerable<T> EmptyIfNull<T>(this IEnumerable<T> items)
        {
            return items ?? new T[0];
        } 
    }
}
