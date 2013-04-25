using System.Threading.Tasks;

namespace RavenFS.Studio.Infrastructure
{
    public static class Schedulers
    {
        public static TaskScheduler UIThread { get; set; }
    }
}
