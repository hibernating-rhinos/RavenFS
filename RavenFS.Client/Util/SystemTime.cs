using System;

namespace RavenFS.Client.Util
{
	public static class SystemTime
	{
		public static Func<DateTime> UtcDateTime;

		public static DateTime UtcNow
		{
			get
			{
				var temp = UtcDateTime;
				return temp == null ? DateTime.UtcNow : temp();
			}
		}
	}
}