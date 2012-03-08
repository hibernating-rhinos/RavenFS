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
    }
}
