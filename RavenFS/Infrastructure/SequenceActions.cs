using RavenFS.Extensions;
using RavenFS.Storage;

namespace RavenFS.Infrastructure
{
	public class SequenceActions
	{
		private const string SequencesKeyPrefix = "Raven/Sequences/";
		private readonly TransactionalStorage _storage;

		public SequenceActions(TransactionalStorage storage)
		{
			_storage = storage;
		}

		public long GetNextValue(string name)
		{
			long result = 1;
			_storage.Batch(
				accessor =>
					{
						var sequenceName = SequenceName(name);
						accessor.TryGetConfigurationValue(sequenceName, out result);
						result++;
						accessor.SetConfigurationValue(sequenceName, result);
					});
			return result;
		}

		private static string SequenceName(string name)
		{
			return SequencesKeyPrefix + name;
		}
	}
}