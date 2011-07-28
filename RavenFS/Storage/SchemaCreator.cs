//-----------------------------------------------------------------------
// <copyright file="SchemaCreator.cs" company="Hibernating Rhinos LTD">
//     Copyright (c) Hibernating Rhinos LTD. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.Text;
using Microsoft.Isam.Esent.Interop;

namespace RavenFS.Storage
{
	[CLSCompliant(false)]
	public class SchemaCreator
	{
		public const string SchemaVersion = "3.6";
		private readonly Session session;

		public SchemaCreator(Session session)
		{
			this.session = session;
		}

		public void Create(string database)
		{
			JET_DBID dbid;
			Api.JetCreateDatabase(session, database, null, out dbid, CreateDatabaseGrbit.None);
			try
			{
				using (var tx = new Transaction(session))
				{
					CreateFilesTable(dbid);
					CreateUsageTable(dbid);
					CreatePagesTable(dbid);
					tx.Commit(CommitTransactionGrbit.None);
				}
			}
			finally
			{
				Api.JetCloseDatabase(session, dbid, CloseDatabaseGrbit.None);
			}
		}

		private void CreatePagesTable(JET_DBID dbid)
		{
			JET_TABLEID tableid;
			Api.JetCreateTable(session, dbid, "pages", 1, 80, out tableid);
			JET_COLUMNID columnid;

			Api.JetAddColumn(session, tableid, "id", new JET_COLUMNDEF
			{
				coltyp = JET_coltyp.Long,
				grbit = ColumndefGrbit.ColumnAutoincrement | ColumndefGrbit.ColumnFixed | ColumndefGrbit.ColumnNotNULL
			}, null, 0, out columnid);

			Api.JetAddColumn(session, tableid, "page_strong_hash", new JET_COLUMNDEF
			{
				coltyp = JET_coltyp.Binary,
				cbMax = 32,
				grbit = ColumndefGrbit.ColumnFixed | ColumndefGrbit.ColumnNotNULL
			}, null, 0, out columnid);

			Api.JetAddColumn(session, tableid, "page_weak_hash", new JET_COLUMNDEF
			{
				coltyp = JET_coltyp.Long,
				grbit = ColumndefGrbit.ColumnFixed | ColumndefGrbit.ColumnNotNULL
			}, null, 0, out columnid);

			Api.JetAddColumn(session, tableid, "data", new JET_COLUMNDEF
			{
				cbMax = 64 * 1024,
				coltyp = JET_coltyp.LongBinary,
				grbit = ColumndefGrbit.ColumnNotNULL
			}, null, 0, out columnid);

			const string indexDef = "+name\0\0";
			Api.JetCreateIndex(session, tableid, "by_name", CreateIndexGrbit.IndexPrimary, indexDef, indexDef.Length,
							   80);
		}
		private void CreateUsageTable(JET_DBID dbid)
		{
			JET_TABLEID tableid;
			Api.JetCreateTable(session, dbid, "usage", 1, 80, out tableid);
			JET_COLUMNID columnid;

			Api.JetAddColumn(session, tableid, "id", new JET_COLUMNDEF
			{
				coltyp = JET_coltyp.Long,
				grbit = ColumndefGrbit.ColumnAutoincrement | ColumndefGrbit.ColumnFixed | ColumndefGrbit.ColumnNotNULL
			}, null, 0, out columnid);

			Api.JetAddColumn(session, tableid, "name", new JET_COLUMNDEF
			{
				cbMax = 1024,
				coltyp = JET_coltyp.LongText,
				cp = JET_CP.Unicode,
				grbit = ColumndefGrbit.ColumnNotNULL
			}, null, 0, out columnid);

			Api.JetAddColumn(session, tableid, "file_pos", new JET_COLUMNDEF
			{
				coltyp = JET_coltyp.Long,
				grbit = ColumndefGrbit.ColumnFixed | ColumndefGrbit.ColumnNotNULL
			}, null, 0, out columnid);

			Api.JetAddColumn(session, tableid, "page_strong_hash", new JET_COLUMNDEF
			{
				coltyp = JET_coltyp.Binary,
				cbMax = 32,
				grbit = ColumndefGrbit.ColumnFixed | ColumndefGrbit.ColumnNotNULL
			}, null, 0, out columnid);

			Api.JetAddColumn(session, tableid, "page_weak_hash", new JET_COLUMNDEF
			{
				coltyp = JET_coltyp.Long,
				grbit = ColumndefGrbit.ColumnFixed | ColumndefGrbit.ColumnNotNULL
			}, null, 0, out columnid);

			const string indexDef = "+name\0\0";
			Api.JetCreateIndex(session, tableid, "by_name", CreateIndexGrbit.IndexPrimary, indexDef, indexDef.Length,
							   80);
		}
		private void CreateFilesTable(JET_DBID dbid)
		{
			JET_TABLEID tableid;
			Api.JetCreateTable(session, dbid, "files", 1, 80, out tableid);
			JET_COLUMNID columnid;

			Api.JetAddColumn(session, tableid, "id", new JET_COLUMNDEF
			{
				coltyp = JET_coltyp.Long,
				grbit = ColumndefGrbit.ColumnAutoincrement|ColumndefGrbit.ColumnFixed |ColumndefGrbit.ColumnNotNULL
			}, null, 0, out columnid);

			Api.JetAddColumn(session, tableid, "name", new JET_COLUMNDEF
			{
				cbMax = 1024,
				coltyp = JET_coltyp.LongText,
				cp = JET_CP.Unicode,
				grbit = ColumndefGrbit.ColumnNotNULL
			}, null, 0, out columnid);

			Api.JetAddColumn(session, tableid, "total_size", new JET_COLUMNDEF
			{
				coltyp = JET_coltyp.Binary,
				cbMax = 8,
				grbit = ColumndefGrbit.ColumnFixed | ColumndefGrbit.ColumnNotNULL
			}, null, 0, out columnid);

			Api.JetAddColumn(session, tableid, "uploaded_size", new JET_COLUMNDEF
			{
				coltyp = JET_coltyp.Binary,
				cbMax = 8,
				grbit = ColumndefGrbit.ColumnFixed | ColumndefGrbit.ColumnNotNULL
			}, null, 0, out columnid);

			const string indexDef = "+name\0\0";
			Api.JetCreateIndex(session, tableid, "by_name", CreateIndexGrbit.IndexPrimary, indexDef, indexDef.Length,
							   80);
		}
	}
}
