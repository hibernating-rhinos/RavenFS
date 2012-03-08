using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using System.Linq;

namespace RavenFS.Studio.Models
{
    public class EditableKeyValueCollection : ObservableCollection<EditableKeyValue>
    {
        public EditableKeyValueCollection(IEnumerable<EditableKeyValue> values) : base(values)
        {
        }

        public static EditableKeyValueCollection FromNameValueCollection(NameValueCollection collection)
        {
            var editableCollection =
                new EditableKeyValueCollection(
                    collection.Select(key => new EditableKeyValue() { Key = key, Value = collection[key]}));

            return editableCollection;
        }

        public NameValueCollection ToNameValueCollection(IList<string> ignoredKeys)
        {
            var collection = new NameValueCollection();

            foreach (var item in Items)
            {
                if (ignoredKeys.Contains(item.Key))
                {
                    continue;
                }

                collection[item.Key] = item.Value;
            }

            return collection;
        }
    }
}
