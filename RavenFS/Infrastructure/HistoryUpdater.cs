using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using RavenFS.Storage;

namespace RavenFS.Infrastructure
{
	using Client;
	using Synchronization;

	public class HistoryUpdater
    {
        private readonly TransactionalStorage storage;
        private readonly ReplicationHiLo replicationHiLo;
        private readonly UuidGenerator uuidGenerator;

        public HistoryUpdater(TransactionalStorage storage, ReplicationHiLo replicationHiLo, UuidGenerator uuidGenerator)
        {
            this.storage = storage;
            this.uuidGenerator = uuidGenerator;
            this.replicationHiLo = replicationHiLo;
        }

        public void Update(string fileName, NameValueCollection nameValueCollection)
        {
            var metadata = GetMetadata(fileName);
            var serverId = metadata[SynchronizationConstants.RavenSynchronizationSource];
            var history = new List<HistoryItem>();
            // if there is RavenReplicationVersion metadata it means that file is not new and we have to add a new item to the history
            if (!String.IsNullOrEmpty(serverId))
            {
                var currentVersion = long.Parse(metadata[SynchronizationConstants.RavenSynchronizationVersion]);
                history = DeserializeHistory(metadata);
                history.Add(new HistoryItem {ServerId = serverId, Version = currentVersion});
            }

			if (history.Count > SynchronizationConstants.ChangeHistoryLength)
			{
				history.RemoveAt(0);
			}

            nameValueCollection[SynchronizationConstants.RavenSynchronizationHistory] = SerializeHistory(history);
            nameValueCollection[SynchronizationConstants.RavenSynchronizationVersion] = replicationHiLo.NextId().ToString(CultureInfo.InvariantCulture);
            nameValueCollection[SynchronizationConstants.RavenSynchronizationSource] = storage.Id.ToString();
        }


        public void UpdateLastModified(NameValueCollection nameValueCollection)
        {
			// internally keep last modified date with milisecond precision
            nameValueCollection["Last-Modified"] = DateTime.UtcNow.ToString("d MMM yyyy H:m:s.fffff 'GMT'", CultureInfo.InvariantCulture);
            nameValueCollection["ETag"] = "\"" + uuidGenerator.CreateSequentialUuid() + "\"";
        }

        private NameValueCollection GetMetadata(string fileName)
        {
            try
            {
                FileAndPages fileAndPages = null;
                storage.Batch(accessor => fileAndPages = accessor.GetFile(fileName, 0, 0));
                return fileAndPages.Metadata;
            } 
            catch(FileNotFoundException)
            {
                return new NameValueCollection();
            }
        }

        public static List<HistoryItem> DeserializeHistory(NameValueCollection nameValueCollection)
        {
            var serializedHistory = nameValueCollection[SynchronizationConstants.RavenSynchronizationHistory];
            return new JsonSerializer().Deserialize<List<HistoryItem>>(new JsonTextReader(new StringReader(serializedHistory)));
        }

        public static string SerializeHistory(List<HistoryItem> history)
        {
            var sb = new StringBuilder();
            var jw = new JsonTextWriter(new StringWriter(sb));
            new JsonSerializer().Serialize(jw, history);
            return sb.ToString();
        }
    }
}