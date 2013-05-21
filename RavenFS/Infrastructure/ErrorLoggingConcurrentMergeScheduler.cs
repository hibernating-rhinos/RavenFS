using System;
using Lucene.Net.Index;
using NLog;

namespace RavenFS.Infrastructure
{
	public class ErrorLoggingConcurrentMergeScheduler : ConcurrentMergeScheduler
	{
		private static readonly Logger Log = LogManager.GetCurrentClassLogger();

		protected override void HandleMergeException(Exception exc)
		{
			try
			{
				base.HandleMergeException(exc);
			}
			catch (Exception e)
			{
				Log.WarnException("Concurrent merge failed", e);
			}
		}
	}
}