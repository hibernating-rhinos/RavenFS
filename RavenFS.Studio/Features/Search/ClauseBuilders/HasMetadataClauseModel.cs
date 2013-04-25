using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using RavenFS.Studio.Infrastructure;
using RavenFS.Studio.Models;
using RavenFS.Studio.Extensions;

namespace RavenFS.Studio.Features.Search.ClauseBuilders
{
    public class HasMetadataClauseModel : ViewModel
    {
        private static readonly HashSet<string> ExcludedTerms = new HashSet<string> { "Content-Length", "Last-Modified" };
 
        string selectedField;
        string searchPattern;

        public HasMetadataClauseModel()
        {
            Initialize();
        }

        private void Initialize()
        {
	        AvailableFields = ApplicationModel.Current.State.CachedSearchTerms == null
		                          ? new ObservableCollection<string>()
		                          : new ObservableCollection<string>(ApplicationModel.Current.State.CachedSearchTerms);

	        ApplicationModel.Current.Client
                .GetSearchFieldsAsync(0, 1024)
                .ContinueOnUIThread(
                    t =>
                        {
                            if (!t.IsFaulted)
                            {
                                AvailableFields.UpdateFromOrdered(
                                    t.Result.Where(term => !term.StartsWith("__") && !ExcludedTerms.Contains(term))
                                        .OrderBy(x => x),
                                    x => x);

                                ApplicationModel.Current.State.CachedSearchTerms =
                                    AvailableFields.ToArray();
                            }
                        });
        }

	    public ObservableCollection<string> AvailableFields { get; set; }

        public string SelectedField
        {
            get { return selectedField; }
            set
            {
                selectedField = value;
                OnPropertyChanged(() => SelectedField);
            }
        }

        public string SearchPattern
        {
            get { return searchPattern; }
            set
            {
                searchPattern = value;
                OnPropertyChanged(() => SearchPattern);
            }
        }
    }
}