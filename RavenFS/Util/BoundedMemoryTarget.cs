using System.Collections.Concurrent;
using System.Collections.Generic;
using NLog;
using NLog.Targets;

namespace RavenFS.Util
{
	public class BoundedMemoryTarget : Target
	{
		private ConcurrentQueue<LogEventInfo> generalLog = new ConcurrentQueue<LogEventInfo>();
		private ConcurrentQueue<LogEventInfo> warnLog = new ConcurrentQueue<LogEventInfo>();

		public IEnumerable<LogEventInfo> GeneralLog
		{
			get { return generalLog; }
		}

		public IEnumerable<LogEventInfo> WarnLog
		{
			get { return warnLog; }
		}

		protected override void Write(LogEventInfo logEvent)
		{
			AddToQueue(logEvent, generalLog);
			if (logEvent.Level >= LogLevel.Warn)
				AddToQueue(logEvent, warnLog);
		}

		private void AddToQueue(LogEventInfo logEvent, ConcurrentQueue<LogEventInfo> logEventInfos)
		{
			logEventInfos.Enqueue(logEvent);
			if (logEventInfos.Count <= 500)
				return;

			LogEventInfo _;
			logEventInfos.TryDequeue(out _);
		}

		public void Clear()
		{
			generalLog = new ConcurrentQueue<LogEventInfo>();
			warnLog = new ConcurrentQueue<LogEventInfo>();
		}
	}
}