using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
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
