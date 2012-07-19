namespace RavenFS.Util
{
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using NLog;
	using NLog.Targets;

	public class BoundedMemoryTarget : Target
	{
		private ConcurrentQueue<LogEventInfo> generalLog = new ConcurrentQueue<LogEventInfo>();
		private ConcurrentQueue<LogEventInfo> warnLog = new ConcurrentQueue<LogEventInfo>();

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

		public IEnumerable<LogEventInfo> GeneralLog
		{
			get { return generalLog; }
		}

		public IEnumerable<LogEventInfo> WarnLog
		{
			get { return warnLog; }
		}

		public void Clear()
		{
			generalLog = new ConcurrentQueue<LogEventInfo>();
			warnLog = new ConcurrentQueue<LogEventInfo>();
		}
	}
}