namespace RavenFS.Client
{
    public class ConfigChange : Notification
    {
        public string Name { get; set; }

        public ConfigChangeAction Action { get; set; }
    }

    public enum ConfigChangeAction
    {
        Set,
        Delete,
    }
}