namespace RavenFS.Studio.Models
{
    public enum AsyncOperationStatus
    {
        Queued,
        Processing,
        Completed,
        Cancelled,
        Error,
    }
}
