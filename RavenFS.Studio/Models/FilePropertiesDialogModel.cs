using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Input;
using RavenFS.Client;
using RavenFS.Studio.Commands;
using RavenFS.Studio.Extensions;
using RavenFS.Studio.Infrastructure;
using System.Linq;
using Raven.Abstractions.Extensions;

namespace RavenFS.Studio.Models
{
	public class FilePropertiesDialogModel : DialogModel
	{
        private EditableKeyValueCollection metadata;
	    private ICommand cancelCommand;
	    private ICommand saveCommand;
	    private EditableKeyValue emptyItem;
	    private ICommand deleteCommand;

	    public string Name { get; set; }

		public FilePropertiesDialogModel()
		{
		}

        protected override void OnViewLoaded()
        {
            if (!string.IsNullOrEmpty(Name))
            {
                ApplicationModel.Current.Client.GetMetadataForAsync(Name)
                .ContinueOnUIThread(UpdateMetadata);
            }

            OnEverythingChanged();
        }

	    public string Title
	    {
	        get { return Name + " properties"; }
	    }

		public EditableKeyValueCollection Metadata
		{
			get { return metadata; }
            private set
            {
                metadata = value;
                OnPropertyChanged();
            }
		}

        private void UpdateMetadata(Task<NameValueCollection> metadata)
        {
            NameValueCollection collection = metadata.Result.FilterHeadersForViewing();

            var editableCollection =
                new EditableKeyValueCollection(
                    collection.Select(key => new EditableKeyValue()
                                                 {
                                                     Key = key, 
                                                     Value = collection[key], 
                                                     IsReadOnly = MetadataExtensions.ReadOnlyHeaders.Contains(key)
                                                 }));

            Metadata = editableCollection;
            AddEmptyItem();
        }

	    private void AddEmptyItem()
	    {
	        if (emptyItem != null)
	        {
	            emptyItem.PropertyChanged -= HandleEmptyItemPropertyChanged;
	        }

            emptyItem = new EditableKeyValue();
	        emptyItem.PropertyChanged += HandleEmptyItemPropertyChanged;

            Metadata.Add(emptyItem);
	    }

	    private void HandleEmptyItemPropertyChanged(object sender, PropertyChangedEventArgs e)
	    {
            // as soon as the user starts modifying the item, it's no longer new, so add a new empty item
            if (!string.IsNullOrEmpty(emptyItem.Key) || !string.IsNullOrEmpty(emptyItem.Value))
            {
                AddEmptyItem();
            }
	    }

        public ICommand DeleteMetadataItemCommand { get { return deleteCommand ?? (deleteCommand = new ActionCommand(HandleDelete)); } }

	    private void HandleDelete(object item)
	    {
	        var metaDataItem = item as EditableKeyValue;
            if (metaDataItem == null || metaDataItem == emptyItem || metaDataItem.IsReadOnly)
            {
                return;
            }

	        Metadata.Remove(metaDataItem);
	    }

	    public ICommand CancelCommand { get { return cancelCommand ?? (cancelCommand = new ActionCommand(() => Close(false))); } }

        public ICommand SaveCommand { get { return saveCommand ?? (saveCommand = new ActionCommand(HandleSave)); } }

	    private void HandleSave()
	    {
	        var newMetaData = Metadata
                .Where(i => i != emptyItem)
                .ToNameValueCollection()
                .FilterHeaders();

            ApplicationModel.Current.AsyncOperations.Do(() =>
                ApplicationModel.Current.Client.UpdateMetadataAsync(Name, newMetaData), "Updating properties for file " + Name);

	        Close(true);
	    }

	}
}