using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using RavenFS.Client;
using RavenFS.Studio.Infrastructure;
using RavenFS.Studio.Extensions;

namespace RavenFS.Studio.Models
{
    public class SearchResultsCollectionSource : VirtualCollectionSource<FileSystemModel>
    {
        public SearchResultsCollectionSource()
        {
            
        }

        public event EventHandler<SearchErrorEventArgs> SearchError;

        private string searchPattern;

        public string SearchPattern
        {
            get { return searchPattern; }
            set
            {
                if (searchPattern == value)
                {
                    return;
                }
                searchPattern = value;
                Refresh(RefreshMode.ClearStaleData);
            }
        }

	    protected override async Task<IList<FileSystemModel>> GetPageAsyncOverride(int start, int pageSize,
	                                                                               IList<SortDescription> sortDescriptions)
	    {
		    var results = await DoQuery(start, pageSize, sortDescriptions);

		    var result = (IList<FileSystemModel>) ToFileSystemModels(results.Files).Take(pageSize).ToArray();
		    SetCount(results.FileCount);
		    return result;
	    }

	    private async Task<SearchResults> DoQuery(int start, int pageSize, IList<SortDescription> sortDescriptions)
        {
		    try
		    {
			    return await ApplicationModel.Current.Client.SearchAsync(searchPattern, MapSortDescription(sortDescriptions),
			                                                             start: start, pageSize: pageSize);

		    }
		    catch (Exception exception)
		    {
			    OnSearchError(new SearchErrorEventArgs {Exception = exception});
			    return null;
		    }
        }

        private string[] MapSortDescription(IEnumerable<SortDescription> sortDescriptions)
        {
            if (sortDescriptions == null)
            {
                return new string[0];
            }

            var sortDescription = sortDescriptions.FirstOrDefault();

            var sort = FilesSortOptions.Default;

            var sortField = "";

            switch (sortDescription.PropertyName)
            {
	            case "Name":
		            sortField = "__fileName";
		            break;
	            case "Size":
		            sortField = "__size";
		            break;
	            case "LastModified":
		            sortField = "__modified";
		            break;
            }

	        if (sortField.Length > 0 && sortDescription.Direction == ListSortDirection.Descending)
		        sortField = "-" + sortField;

	        return !sortField.IsNullOrEmpty() ? new[] {sortField} : new string[0];
        }

        private static IEnumerable<FileSystemModel> ToFileSystemModels(IEnumerable<FileInfo> files)
        {
            return files
                .Where(fi => fi != null)
                .Select(fi => new FileModel
                                  {
                                      FormattedTotalSize = fi.HumaneTotalSize,
                                      FullPath = fi.Name,
                                      Metadata = fi.Metadata
                                  });
        }

        protected void OnSearchError(SearchErrorEventArgs e)
        {
            var handler = SearchError;
            if (handler != null) handler(this, e);
        }
    }

    public class SearchErrorEventArgs : EventArgs
    {
        public Exception Exception { get; set; }
    }
}
