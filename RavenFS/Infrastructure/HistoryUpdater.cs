using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using Newtonsoft.Json;
using RavenFS.Rdc;
using RavenFS.Storage;

namespace RavenFS.Infrastructure
{
    public class HistoryUpdater
    {
        private readonly TransactionalStorage _storage;
        private readonly ReplicationHiLo _replicationHiLo;

        public HistoryUpdater(TransactionalStorage storage, ReplicationHiLo replicationHiLo)
        {
            _storage = storage;
            _replicationHiLo = replicationHiLo;
        }

        public void Update(string fileName, NameValueCollection nameValueCollection)
        {
            var metadata = GetMetadata(fileName);
            var serverId = metadata[ReplicationConstants.RavenReplicationSource];
            var history = new List<HistoryItem>();
            // if there is RavenReplicationVersion metadata it means that file is not new and we have to add a new item to the history
            if (!String.IsNullOrEmpty(serverId))
            {
                var currentVersion = long.Parse(metadata[ReplicationConstants.RavenReplicationVersion]);
                history = DeserializeHistory(metadata);
                history.Add(new HistoryItem {ServerId = serverId, Version = currentVersion});
            }
            nameValueCollection[ReplicationConstants.RavenReplicationHistory] = SerializeHistory(history);
            nameValueCollection[ReplicationConstants.RavenReplicationVersion] = _replicationHiLo.NextId().ToString();
            nameValueCollection[ReplicationConstants.RavenReplicationSource] = _storage.Id.ToString();
        }

        private NameValueCollection GetMetadata(string fileName)
        {
            try
            {
                FileAndPages fileAndPages = null;
                _storage.Batch(accessor => fileAndPages = accessor.GetFile(fileName, 0, 0));
                return fileAndPages.Metadata;
            } 
            catch(FileNotFoundException)
            {
                return new NameValueCollection();
            }
        }

        public static List<HistoryItem> DeserializeHistory(NameValueCollection nameValueCollection)
        {
            var serializedHistory = nameValueCollection[ReplicationConstants.RavenReplicationHistory];
            return new JsonSerializer().Deserialize<List<HistoryItem>>(new JsonTextReader(new StringReader(serializedHistory)));
        }

        private static string SerializeHistory(List<HistoryItem> history)
        {
            var sb = new StringBuilder();
            var jw = new JsonTextWriter(new StringWriter(sb));
            new JsonSerializer().Serialize(jw, history);
            return sb.ToString();
        }
    }
}