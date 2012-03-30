using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
        public event EventHandler<EventArgs> KeyValueChanged;

        public EditableKeyValueCollection()
        {
            
        }

        public EditableKeyValueCollection(IEnumerable<EditableKeyValue> values) : base(values)
        {
            foreach (var item in this)
            {
                AttachPropertyChangeHandler(item);
            }
        }

        protected override void InsertItem(int index, EditableKeyValue item)
        {
            AttachPropertyChangeHandler(item);
            base.InsertItem(index, item);
        }

        private void AttachPropertyChangeHandler(EditableKeyValue item)
        {
            item.PropertyChanged += HandlePropertyChanged;
        }

        protected override void RemoveItem(int index)
        {
            var item = this[index];

            item.PropertyChanged -= HandlePropertyChanged;

            base.RemoveItem(index);
        }

        private void HandlePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            OnKeyValueChanged(EventArgs.Empty);
        }

        protected void OnKeyValueChanged(EventArgs e)
        {
            EventHandler<EventArgs> handler = KeyValueChanged;
            if (handler != null) handler(this, e);
        }
    }
}
