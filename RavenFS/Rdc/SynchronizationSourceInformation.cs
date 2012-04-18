namespace RavenFS.Rdc
{
	using System;

	public class SynchronizationSourceInformation
	{
		public Guid LastDocumentEtag { get; set; }
		public Guid ServerInstanceId { get; set; }
	}
}