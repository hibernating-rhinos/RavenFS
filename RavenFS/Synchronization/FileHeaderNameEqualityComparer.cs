namespace RavenFS.Synchronization
{
	using System.Collections.Generic;
	using Storage;

	internal class FileHeaderNameEqualityComparer : IEqualityComparer<FileHeader>
	{
		public bool Equals(FileHeader x, FileHeader y)
		{
			return x.Name == y.Name;
		}

		public int GetHashCode(FileHeader header)
		{
			return (header.Name != null ? header.Name.GetHashCode() : 0);
		}
	}
}