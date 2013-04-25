using System;

namespace RavenFS.Studio.Models
{
    public class FileSystemModel
    {
        private string fullPath;

        public string Name { get; private set; }

        public string Folder
        {
            get; private set;
        }

        public string FullPath
        {
            get { return fullPath; }
            set
            {
                fullPath = value;
                var lastSlash = fullPath.LastIndexOf("/", StringComparison.InvariantCulture);

                if (lastSlash > 0)
                {
                    Name = fullPath.Substring(lastSlash + 1);
                    Folder = fullPath.Substring(0, lastSlash);
	                if (!Folder.StartsWith("/"))
		                Folder = "/" + Folder;
                }
                else if (lastSlash == 0)
                {
                    Name = fullPath.TrimStart('/');
                    Folder = "/";
                }
                else
                {
                    Name = fullPath;
                    Folder = "/";
                }
            }
        }
    }
}
