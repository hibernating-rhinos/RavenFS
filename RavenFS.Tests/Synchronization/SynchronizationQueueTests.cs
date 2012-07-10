namespace RavenFS.Tests.Synchronization
{
	using System;
	using System.Collections.Specialized;
	using System.Linq;
	using RavenFS.Synchronization;
	using Xunit;

	public class SynchronizationQueueTests : WebApiTest
	{
		[Fact]
		public void Should_not_enqueue_synchronization_if_the_same_work_is_active()
		{
			var fileMetadata = new NameValueCollection
			                   	{
			                   		{"ETag", "\"" + Guid.NewGuid() + "\""}
			                   	};

			var serverId = Guid.NewGuid();

			var queue = new SynchronizationQueue();

			queue.EnqueueSynchronization("http://dest",
			                             new MetadataUpdateWorkItem("test.txt", fileMetadata,new NameValueCollection(), serverId));

			SynchronizationWorkItem work;

			queue.TryDequeuePendingSynchronization("http://dest", out work);
			queue.SynchronizationStarted(work, "http://dest");

			// attempt to enqueue the same work
			queue.EnqueueSynchronization("http://dest",
										 new MetadataUpdateWorkItem("test.txt", fileMetadata, new NameValueCollection(), serverId));

			Assert.Equal(1, queue.Active.Count());
			Assert.Equal(0, queue.Pending.Count());
		}

		[Fact]
		public void Should_enqueue_to_pending_if_work_of_the_same_type_but_with_different_etag_is_active()
		{
			var fileMetadata = new NameValueCollection
			                   	{
			                   		{"ETag", "\"" + Guid.NewGuid() + "\""}
			                   	};

			var serverId = Guid.NewGuid();

			var queue = new SynchronizationQueue();

			queue.EnqueueSynchronization("http://dest",
										 new MetadataUpdateWorkItem("test.txt", fileMetadata, new NameValueCollection(), serverId));

			SynchronizationWorkItem work;

			queue.TryDequeuePendingSynchronization("http://dest", out work);
			queue.SynchronizationStarted(work, "http://dest");

			queue.EnqueueSynchronization("http://dest",
			                             new MetadataUpdateWorkItem("test.txt", new NameValueCollection
			                                                                    	{
			                                                                    		{"ETag", "\"" + Guid.NewGuid() + "\""} // new different ETag
			                                                                    	}, new NameValueCollection(), serverId));

			Assert.Equal(1, queue.Active.Count());
			Assert.Equal(1, queue.Pending.Count());
		}
	}
}