namespace RavenFS.Tests.Synchronization
{
	using System;
	using System.Collections.Specialized;
	using System.Linq;
	using RavenFS.Synchronization;
	using RavenFS.Synchronization.Rdc.Wrapper;
	using Xunit;

	public class SynchronizationQueueTests : StorageTest
	{
		private static readonly NameValueCollection EmptyETagMetadata = new NameValueCollection
		                                                            	{
		                                                            		{"ETag", "\"" + Guid.Empty + "\""}
		                                                            	};
		private readonly SynchronizationQueue queue;
		private const string Destination = "http://dest";
		private const string FileName = "test.txt";

		public SynchronizationQueueTests()
		{
			queue = new SynchronizationQueue();
		}

		[Fact]
		public void Should_not_enqueue_synchronization_if_the_same_work_is_active()
		{
			transactionalStorage.Batch(accessor => accessor.PutFile(FileName, 0, EmptyETagMetadata));

			queue.EnqueueSynchronization(Destination,
			                             new MetadataUpdateWorkItem(FileName, new NameValueCollection(), transactionalStorage));

			SynchronizationWorkItem work;

			queue.TryDequeuePendingSynchronization(Destination, out work);
			queue.SynchronizationStarted(work, Destination);

			// attempt to enqueue the same work
			queue.EnqueueSynchronization(Destination,
										 new MetadataUpdateWorkItem(FileName, new NameValueCollection(), transactionalStorage));

			Assert.Equal(1, queue.Active.Count());
			Assert.Equal(0, queue.Pending.Count());
		}

		[Fact]
		public void Should_enqueue_to_pending_if_work_of_the_same_type_but_with_different_etag_is_active()
		{
			transactionalStorage.Batch(accessor => accessor.PutFile(FileName, 0, EmptyETagMetadata));

			queue.EnqueueSynchronization(Destination,
										 new MetadataUpdateWorkItem(FileName, new NameValueCollection(), transactionalStorage));

			SynchronizationWorkItem work;

			queue.TryDequeuePendingSynchronization(Destination, out work);
			queue.SynchronizationStarted(work, Destination);

			transactionalStorage.Batch(accessor => accessor.UpdateFileMetadata(FileName, new NameValueCollection
				                                                                               	{
				                                                                               		{"ETag", "\"" + Guid.NewGuid() + "\""}
				                                                                               	}));

			var metadataUpdateWorkItem = new MetadataUpdateWorkItem(FileName, new NameValueCollection(), transactionalStorage);

			metadataUpdateWorkItem.RefreshMetadata();

			queue.EnqueueSynchronization(Destination, metadataUpdateWorkItem);

			Assert.Equal(1, queue.Active.Count());
			Assert.Equal(1, queue.Pending.Count());
		}

		[MtaFact]
		public void Should_be_only_work_with_greater_etag_in_pending_queue()
		{
			using (var sigGenerator = new SigGenerator())
			{
				transactionalStorage.Batch(accessor => accessor.PutFile(FileName, 0, EmptyETagMetadata));

				queue.EnqueueSynchronization(Destination,
											 new ContentUpdateWorkItem(FileName, transactionalStorage, sigGenerator));

				Assert.Equal(1, queue.Pending.Count());

				var greaterGuid = Guid.NewGuid();

				transactionalStorage.Batch(accessor => accessor.UpdateFileMetadata(FileName, new NameValueCollection
				                                                                               	{
				                                                                               		{"ETag", "\"" + greaterGuid + "\""}
				                                                                               	}));

				queue.EnqueueSynchronization(Destination,
											 new ContentUpdateWorkItem(FileName, transactionalStorage, new SigGenerator()));

				Assert.Equal(1, queue.Pending.Count());
				Assert.Equal(greaterGuid, queue.Pending.ToArray()[0].FileETag);
			}
		}

		[MtaFact]
		public void Should_detect_that_different_work_is_being_perfomed()
		{
			using (var sigGenerator = new SigGenerator())
			{
				transactionalStorage.Batch(accessor => accessor.PutFile(FileName, 0, EmptyETagMetadata));

				var contentUpdateWorkItem = new ContentUpdateWorkItem(FileName, transactionalStorage, sigGenerator);
				
				queue.EnqueueSynchronization(Destination, contentUpdateWorkItem);
				queue.SynchronizationStarted(contentUpdateWorkItem, Destination);

				Assert.True(queue.IsDifferentWorkForTheSameFileBeingPerformed(
					new RenameWorkItem(FileName, "rename.txt", transactionalStorage), Destination));
			}
		}
	}
}