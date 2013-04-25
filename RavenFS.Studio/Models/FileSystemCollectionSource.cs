using RavenFS.Studio.Infrastructure;

namespace RavenFS.Studio.Models
{
    public class FileSystemCollectionSource : CompositeVirtualCollectionSource<FileSystemModel>
    {
        public FileSystemCollectionSource() : base(new FoldersCollectionSource(), new FilesCollectionSource())
        {
            
        } 

        public string CurrentFolder
        {
            get
            {
                return FoldersCollection.CurrentFolder;
            }
            set
            {
                FoldersCollection.CurrentFolder = value;
                FilesCollection.CurrentFolder = value;
            }
        }

        private FoldersCollectionSource FoldersCollection
        {
            get { return Source1 as FoldersCollectionSource; }
        }

        private FilesCollectionSource FilesCollection
        {
            get { return Source2 as FilesCollectionSource; }
        }

        public string SearchPattern
        {
            get { return FoldersCollection.SearchPattern; }
            set
            {
                FoldersCollection.SearchPattern = value;
                FilesCollection.SearchPattern = value;
            }
        }
    }
}
