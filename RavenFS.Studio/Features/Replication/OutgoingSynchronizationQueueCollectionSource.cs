using RavenFS.Client;
using RavenFS.Studio.Infrastructure;

namespace RavenFS.Studio.Features.Replication
{
    public class OutgoingSynchronizationQueueCollectionSource : CompositeVirtualCollectionSource<SynchronizationDetails>
    {
        public OutgoingSynchronizationQueueCollectionSource() : base(new ActiveSynchronizationTasksCollectionSource(), new PendingSynchronizationTasksCollectionSource())
        {
        }
    }
}
