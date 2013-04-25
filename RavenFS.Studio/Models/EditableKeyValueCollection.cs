using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

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
            var handler = KeyValueChanged;
            if (handler != null) handler(this, e);
        }
    }
}
