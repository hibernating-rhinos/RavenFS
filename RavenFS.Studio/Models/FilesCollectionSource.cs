using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using RavenFS.Client;
using RavenFS.Studio.Infrastructure;

namespace RavenFS.Studio.Models
{
    public class FilesCollectionSource : VirtualCollectionSource<FileSystemModel>
    {
        private string currentFolder;
        private string searchPattern;

        public FilesCollectionSource()
        {
            currentFolder = "/";
        }

        public string CurrentFolder
        {
            get { return currentFolder; }
            set
            {
                currentFolder = value;
                Refresh(RefreshMode.ClearStaleData);
            }
        }

        public string SearchPattern
        {
            get { return searchPattern; }
            set
            {
	            if (searchPattern == value)
		            return;
	           
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

	    private Task<SearchResults> DoQuery(int start, int pageSize, IList<SortDescription> sortDescriptions)
        {
            return ApplicationModel.Current.Client
                .GetFilesAsync(currentFolder, MapSortDescription(sortDescriptions), fileNameSearchPattern:searchPattern, start: start, pageSize: pageSize)
                .Catch("Could not fetch files from server");
        }

        private FilesSortOptions MapSortDescription(IList<SortDescription> sortDescriptions)
        {
	        if (sortDescriptions == null)
		        return FilesSortOptions.Default;

	        var sortDescription = sortDescriptions.FirstOrDefault();

            var sort = FilesSortOptions.Default;

	        switch (sortDescription.PropertyName)
	        {
		        case "Name":
			        sort = FilesSortOptions.Name;
			        break;
		        case "Size":
			        sort = FilesSortOptions.Size;
			        break;
		        case "LastModified":
			        sort = FilesSortOptions.LastModified;
			        break;
	        }

	        if (sortDescription.Direction == ListSortDirection.Descending)
		        sort |= FilesSortOptions.Desc;

	        return sort;
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
    }
}
