namespace RavenFS.Client
{
	using System;

	public class SourceSynchronizationInformation
	{
		public Guid LastSourceFileEtag { get; set; }
		public string SourceServerUrl { get; set; }
		public Guid DestinationServerInstanceId { get; set; }

		public override string ToString()
		{
			return string.Format("LastSourceFileEtag: {0}", LastSourceFileEtag);
		}
	}
}