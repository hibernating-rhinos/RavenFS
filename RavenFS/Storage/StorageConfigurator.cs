//-----------------------------------------------------------------------
// <copyright file="TransactionalStorageConfigurator.cs" company="Hibernating Rhinos LTD">
//     Copyright (c) Hibernating Rhinos LTD. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.Collections.Specialized;
using System.IO;
using Microsoft.Isam.Esent.Interop;

namespace RavenFS.Storage
{
	public class StorageConfigurator
	{
		public const int MaxSessions = 256;

		private readonly NameValueCollection settings;

		public StorageConfigurator(NameValueCollection settings)
		{
			this.settings = settings;
		}


		public void ConfigureInstance(JET_INSTANCE jetInstance, string path)
		{
			path = Path.GetFullPath(path);
			new InstanceParameters(jetInstance)
			{
				CircularLog = true,
				Recovery = true,
				NoInformationEvent = false,
				CreatePathIfNotExist = true,
				TempDirectory = Path.Combine(path, "temp"),
				SystemDirectory = Path.Combine(path, "system"),
				LogFileDirectory = Path.Combine(path, "logs"),
				MaxVerPages = TranslateToSizeInDatabasePages(GetValueFromConfiguration("Raven/Esent/MaxVerPages", 128)),
				BaseName = "RFS",
				EventSource = "RavenFS",
				LogBuffers = TranslateToSizeInDatabasePages(GetValueFromConfiguration("Raven/Esent/LogBuffers", 16)) / 2,
				LogFileSize = GetValueFromConfiguration("Raven/Esent/LogFileSize", 16) * 1024,
				MaxSessions = MaxSessions,
				MaxCursors = GetValueFromConfiguration("Raven/Esent/MaxCursors", 2048),
				DbExtensionSize = TranslateToSizeInDatabasePages(GetValueFromConfiguration("Raven/Esent/DbExtensionSize", 16)),
				AlternateDatabaseRecoveryDirectory = path
			};
		}

		public void LimitSystemCache()
		{
			int cacheSizeMaxInMegabytes = GetValueFromConfiguration("Raven/Esent/CacheSizeMax",1024);
			int cacheSizeMax = TranslateToSizeInDatabasePages(cacheSizeMaxInMegabytes);
			if (SystemParameters.CacheSizeMax > cacheSizeMax)
			{
				try
				{
					SystemParameters.CacheSizeMax = cacheSizeMax;
				}
				catch (Exception) // this case fail if we do it for the second time, we can just ignore this, then
				{
				}
			}
		}

		private static int TranslateToSizeInDatabasePages(int sizeInMegabytes)
		{
			return (sizeInMegabytes * 1024 * 1024) / SystemParameters.DatabasePageSize;
		}

		private int GetValueFromConfiguration(string name, int defaultValue)
		{
			int value;
			if (string.IsNullOrEmpty(settings[name]) == false &&
				int.TryParse(settings[name], out value))
			{
				return value;
			}
			return defaultValue;
		}
	}
}
