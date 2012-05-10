namespace RavenFS.Tests.RDC
{
	using System.Collections.Specialized;
	using System.Threading;
	using Rdc;
	using Tools;
	using Xunit;
	using Xunit.Extensions;

	public class TriggeredSynchronizationTests : MultiHostTestBase
	{
		[Theory]
		[InlineData(1024 * 1024)]
		public void Should_synchronize_to_destinations(int size) //should detect what files require synchronization
		{
			var sourceContent = new RandomStream(size);
			var sourceClient = NewClient(1);

			var destinationClient = NewClient(0);

			sourceClient.Config.SetConfig(SynchronizationConstants.RavenReplicationDestinations, new NameValueCollection
			                                                                                     	{
			                                                                                     		{ "url", destinationClient.ServerUrl }
			                                                                                     	}).Wait();

			sourceClient.UploadAsync("test.bin", new NameValueCollection(), sourceContent).Wait();



			sourceClient.Synchronization.SynchronizeDestinationsAsync();


			//For debugging purposes
			Thread.Sleep(100000000);
		}
	}
}