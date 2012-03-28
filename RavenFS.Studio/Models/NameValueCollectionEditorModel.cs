using System;
using System.ComponentModel;
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
using RavenFS.Client;
using RavenFS.Studio.Extensions;
using RavenFS.Studio.Infrastructure;

namespace RavenFS.Studio.Models
{
    public class NameValueCollectionEditorModel : Model
    {
        private EditableKeyValue emptyItem;
        private ICommand deleteCommand;

        public event EventHandler<EventArgs> Changed;

        public NameValueCollectionEditorModel(NameValueCollection settings)
        {
            Initialise(settings);
        }

        private void Initialise(NameValueCollection settings)
        {
            var editableCollection =
                new EditableKeyValueCollection(
                    settings.Select(key => new EditableKeyValue()
                                                 {
                                                     Key = key,
                                                     Value = settings[key],
                                                 }));

            editableCollection.KeyValueChanged += delegate { OnChanged(EventArgs.Empty); };
            editableCollection.CollectionChanged += delegate { OnChanged(EventArgs.Empty); };

            EditableValues = editableCollection;
            AddEmptyItem();
        }

        public ICommand DeleteMetadataItemCommand { get { return deleteCommand ?? (deleteCommand = new ActionCommand(HandleDelete)); } }

        public EditableKeyValueCollection EditableValues { get; private set; }

        public NameValueCollection GetCurrent()
        {
            return EditableValues
                .Where(i => i != emptyItem)
                .ToNameValueCollection();
        }

        private void AddEmptyItem()
        {
            if (emptyItem != null)
            {
                emptyItem.PropertyChanged -= HandleEmptyItemPropertyChanged;
            }

            emptyItem = new EditableKeyValue();
            emptyItem.PropertyChanged += HandleEmptyItemPropertyChanged;

            EditableValues.Add(emptyItem);
        }

        private void HandleDelete(object item)
        {
            var metaDataItem = item as EditableKeyValue;
            if (metaDataItem == null || metaDataItem == emptyItem || metaDataItem.IsReadOnly)
            {
                return;
            }

            EditableValues.Remove(metaDataItem);
        }

        private void HandleEmptyItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // as soon as the user starts modifying the item, it's no longer new, so add a new empty item
            if (!string.IsNullOrEmpty(emptyItem.Key) || !string.IsNullOrEmpty(emptyItem.Value))
            {
                AddEmptyItem();
            }
        }

        protected void OnChanged(EventArgs e)
        {
            EventHandler<EventArgs> handler = Changed;
            if (handler != null) handler(this, e);
        }
    }
}
