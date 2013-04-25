using RavenFS.Client;

namespace RavenFS.Studio.Models
{
    public class FileModel : FileSystemModel
    {
        public string FormattedTotalSize { get; set; }
        public NameValueCollection Metadata { get; set; }
    }
}
