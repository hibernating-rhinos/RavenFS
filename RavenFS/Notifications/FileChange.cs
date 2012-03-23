namespace RavenFS.Notifications
{
    public class FileChange
    {
        public string File { get; set; }

        public FileChangeAction Action { get; set; }
    }

    public enum FileChangeAction
    {
        Add,
        Delete,
        Update,
        Rename
    }
}