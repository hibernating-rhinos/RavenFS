namespace RavenFS.Infrastructure
{
	using RavenFS.Extensions;
	using RavenFS.Storage;

	public class SequenceActions
    {
        private readonly TransactionalStorage _storage;
        private const string SequencesKeyPrefix = "Raven/Sequences/";

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