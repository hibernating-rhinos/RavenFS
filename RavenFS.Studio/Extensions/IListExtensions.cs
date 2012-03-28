using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace RavenFS.Studio.Extensions
{
    public static class IListExtensions
    {
        public static IEnumerable<T> Skip<T>(this IList<T> list, int count)
        {
            for (int i = count; i < list.Count; i++)
            {
                yield return list[i];
            }
        }

        public static int IndexOf<T>(this IList<T> list, Func<T, bool> predicate)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (predicate(list[i]))
                {
                    return i;
                }
            }

            return -1;
        }

        public static void AddRange<T>(this IList<T> list, IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                list.Add(item);
            }
        }

        /// <summary>
        /// Ensures that the target list contains the same elements as the source list without removing
        /// any elements that are in both. Assumes that the ordering of items within both lists is the same.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="targetList"></param>
        /// <param name="items"></param>
        public static void UpdateFromOrdered<T,TKey>(this IList<T> targetList, IEnumerable<T> items, Func<T, TKey> keySelector)
        {
            var sourceList = items.ToList();

            var removedIndices = targetList.Select(keySelector)
                .Except(sourceList.Select(keySelector))
                .Select(key => targetList.IndexOf(t => keySelector(t).Equals(key)))
                .ToList();

            foreach (var index in removedIndices)
            {
                targetList.RemoveAt(index);
            }

            for (int i = 0; i < sourceList.Count; i++)
            {
                if (targetList.Count <= i)
                {
                    targetList.Add(sourceList[i]);
                }
                else if (!keySelector(targetList[i]).Equals(keySelector(sourceList[i])))
                {
                    targetList.Insert(i, sourceList[i]);
                }
            }
        }
    }
}
