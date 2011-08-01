//-----------------------------------------------------------------------
// <copyright file="StorageActionsAccessor.cs" company="Hibernating Rhinos LTD">
//     Copyright (c) Hibernating Rhinos LTD. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.Security.Cryptography;
using Microsoft.Isam.Esent.Interop;
using RavenFS.Util;

namespace RavenFS.Storage
{
	[CLSCompliant(false)]
	public class StorageActionsAccessor : IDisposable, IStorageActions
	{
		private readonly TableColumnsCache tableColumnsCache;
		private readonly Session session;
		private readonly JET_DBID database;

		private Table files, usage, pages;
		private readonly Transaction transaction;

		private Table Files
		{
			get { return files ?? (files = new Table(session, database, "files", OpenTableGrbit.None)); }
		}

		private Table Usage
		{
			get { return usage ?? (usage = new Table(session, database, "usage", OpenTableGrbit.None)); }
		}

		private Table Pages
		{
			get { return pages ?? (pages = new Table(session, database, "pages", OpenTableGrbit.None)); }
		}

		public StorageActionsAccessor(TableColumnsCache tableColumnsCache, JET_INSTANCE instance, string databaseName)
		{
			this.tableColumnsCache = tableColumnsCache;
			try
			{
				session = new Session(instance);
				transaction = new Transaction(session);
				Api.JetOpenDatabase(session, databaseName, null, out database, OpenDatabaseGrbit.None);
			}
			catch (Exception)
			{
				Dispose();
				throw;
			}
		}
		
		public void Dispose()
		{
			if(pages != null)
				pages.Dispose();
			if (usage != null)
				usage.Dispose();
			if(files != null)
				files.Dispose();
			if(Equals(database, JET_DBID.Nil) == false)
				Api.JetCloseDatabase(session, database, CloseDatabaseGrbit.None);
			if(transaction != null)
				transaction.Dispose();
			if(session != null)
				session.Dispose();
		}

		public void Commit()
		{
			transaction.Commit(CommitTransactionGrbit.None);
		}

		public HashKey InsertPage(byte[] buffer, int size)
		{
			var key = new HashKey(buffer, size);

			using(var update = new Update(session, Pages, JET_prep.Insert))
			{
				Api.SetColumn(session, Pages, tableColumnsCache.PagesColumns["page_strong_hash"],key.Strong);
				Api.SetColumn(session, Pages, tableColumnsCache.PagesColumns["page_weak_hash"], key.Weak);
				Api.JetSetColumn(session, Pages, tableColumnsCache.PagesColumns["data"], buffer, size,
				                 SetColumnGrbit.None, null);

				update.Save();
			}
			return key;
		}

		public void PutFile(string filename, long totalSize)
		{
			throw new NotImplementedException();
		}

		public void AssociatePage(string filename, HashKey pageKey)
		{
			throw new NotImplementedException();
		}

		public int ReadPage(HashKey key, byte[] buffer, int index)
		{
			Api.JetSetCurrentIndex(session, Pages, "by_keys");
			Api.MakeKey(session, Pages, key.Weak,MakeKeyGrbit.NewKey);
			Api.MakeKey(session, Pages,key.Strong, MakeKeyGrbit.None);

			if (Api.TrySeek(session, Pages, SeekGrbit.SeekEQ) == false)
				return -1;

			int size;
			Api.JetRetrieveColumn(session, Pages, tableColumnsCache.PagesColumns["data"], buffer, buffer.Length - index, out size,
			                      RetrieveColumnGrbit.None, null);
			return size;
		}
	}

}
