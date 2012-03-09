using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Runtime.CompilerServices;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Lucene.Net.Store;
using RavenFS.Extensions;
using RavenFS.Infrastructure;
using Version = Lucene.Net.Util.Version;

namespace RavenFS.Search
{
	public class IndexStorage : IDisposable
	{
		private readonly string path;
		private FSDirectory directory;
		private LowerCaseKeywordAnalyzer analyzer;
		private IndexWriter writer;
		private readonly object writerLock = new object();
		private IndexSearcher searcher;

		public IndexStorage(string path, NameValueCollection _)
		{
			this.path = Path.Combine(path.ToFullPath(), "Index.ravenfs");
		}

		public void Initialize()
		{
			directory = FSDirectory.Open(new DirectoryInfo(path));
			if (IndexWriter.IsLocked(directory))
				IndexWriter.Unlock(directory);

			analyzer = new LowerCaseKeywordAnalyzer();
			writer = new IndexWriter(directory, analyzer, IndexWriter.MaxFieldLength.UNLIMITED);
			writer.SetMergeScheduler(new ErrorLoggingConcurrentMergeScheduler());
			searcher = new IndexSearcher(writer.GetReader());
		}

		public string[] Query(string query, int start, int pageSize)
		{
			var queryParser = new QueryParser(Version.LUCENE_29, "", analyzer);
			var q = queryParser.Parse(query);

			var topDocs = searcher.Search(q, pageSize + start);

			var results = new List<string>();

			for (var i = start; i < pageSize + start && i < topDocs.totalHits; i++)
			{
				var document = searcher.Doc(topDocs.scoreDocs[i].doc);
				results.Add(document.Get("__key"));
			}
			return results.ToArray();
		}

		public void Index(string key, NameValueCollection metadata)
		{
			lock (writerLock)
			{
				var doc = new Document();

				string lowerKey = key.ToLowerInvariant();
				doc.Add(new Field("__key", lowerKey, Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));
				var directoryName = Path.GetDirectoryName(key);
				if (string.IsNullOrEmpty(directoryName))
					directoryName = "/";
				else
					directoryName = directoryName.Replace("\\", "/");

				doc.Add(new Field("__directory", directoryName.ToLowerInvariant(),Field.Store.NO, Field.Index.NOT_ANALYZED_NO_NORMS));

				foreach (var metadataKey in metadata.AllKeys)
				{
					var values = metadata.GetValues(metadataKey);
					if(values == null)
						continue;

					foreach (var value in values)
					{
						doc.Add(new Field(metadataKey, value, Field.Store.NO, Field.Index.ANALYZED_NO_NORMS));
					}
				}

				writer.DeleteDocuments(new Term("__key", lowerKey));
				writer.AddDocument(doc);
				// yes, this is slow, but we aren't expecting high writes count
				writer.Commit();
				ReplaceSearcher();
			}
		}
		public void Dispose()
		{
			analyzer.Close();
			searcher.GetIndexReader().Close();
			searcher.Close();
			writer.Close();
			directory.Close();
		}

		public void Delete(string key)
		{
			lock (writerLock)
			{
				writer.DeleteDocuments(new Term("__key", key));
				writer.Optimize();
				writer.Commit();
				ReplaceSearcher();
			}
		}

		private void ReplaceSearcher()
		{
			var currentSearcher = searcher;
			currentSearcher.GetIndexReader().Close();
			currentSearcher.Close();

			searcher = new IndexSearcher(writer.GetReader());
		}

		public IEnumerable<string> GetTermsFor(string field, string fromValue, int pageSize)
		{
			var result = new HashSet<string>();
			var termEnum = searcher.GetIndexReader().Terms(new Term(field, fromValue ?? string.Empty));
			try
			{
				if (string.IsNullOrEmpty(fromValue) == false) // need to skip this value
				{
					while (termEnum.Term() == null || fromValue.Equals(termEnum.Term().Text()))
					{
						if (termEnum.Next() == false)
							return result;
					}
				}
				while (termEnum.Term() == null ||
					field.Equals(termEnum.Term().Field()))
				{
					if (termEnum.Term() != null)
						result.Add(termEnum.Term().Text());

					if (result.Count >= pageSize)
						break;

					if (termEnum.Next() == false)
						break;
				}
			}
			finally
			{
				termEnum.Close();
			}
			return result;
		}
	}
}