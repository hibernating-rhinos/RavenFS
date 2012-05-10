namespace RavenFS.Client
{
	using System;

	public class SourceSynchronizationInformation
	{
		public Guid LastSourceFileEtag { get; set; }
		public Guid DestinationServerInstanceId { get; set; }
	}
}