using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using RavenFS.Client;
using RavenFS.Studio.Commands;
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
			set { metadata = value; OnPropertyChanged();}
		}

        private void UpdateMetadata(Task<NameValueCollection> metadata)
        {
            Metadata = EditableKeyValueCollection.FromNameValueCollection(metadata.Result.FilterHeadersForViewing());
        }

        public ICommand CancelCommand { get { return cancelCommand ?? (cancelCommand = new ActionCommand(() => Close(false))); } }

        public ICommand SaveCommand { get { return saveCommand ?? (saveCommand = new ActionCommand(HandleSave)); } }

	    private void HandleSave()
	    {
	        var newMetaData = Metadata.ToNameValueCollection(GetNonEditableKeys()).FilterHeaders();

            ApplicationModel.Current.AsyncOperations.Do(() =>
                ApplicationModel.Current.Client.UpdateMetadataAsync(Name, newMetaData), "Updating properties for file " + Name);

	        Close(true);

	    }

        private IList<string> GetNonEditableKeys()
        {
            return new[]
                       {
                           "Accept",
                           "Connection",
                           "Content-Type",
                           "Content-Length",
                           "Date",
                           "Expect",
                           "Host",
                           "If-Modified-Since",
                           "Proxy-Connection",
                           "Range",
                           "Referer",
                           "Transfer-Encoding",
                           "User-Agent",
                       };
        }
	}
}