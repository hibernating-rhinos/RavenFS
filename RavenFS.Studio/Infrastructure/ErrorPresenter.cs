using System;
using System.Diagnostics;

namespace RavenFS.Studio.Infrastructure
{
	public static class ErrorPresenter
	{
		private static bool isErrorWindowVisible;

		public static void Show(Uri uri, Exception e)
		{
			var message = string.Format("Could not load page: {0}. {2}Error Message: {1}", uri, e.Message, Environment.NewLine);
			Show(message, e.ToString());
		}

		public static void Show(Exception e)
		{
			Show(e.Message, e.StackTrace);
		}

		public static void Show(Exception e, StackTrace innerStackTrace)
		{
			var details = e +
						  Environment.NewLine + Environment.NewLine +
						  "Inner StackTrace: " + Environment.NewLine +
						  (innerStackTrace == null ? "null" : innerStackTrace.ToString());
			Show(e.Message, details);
		}

		public static void Show(string message, string details)
		{
			if (isErrorWindowVisible)
				return;

			isErrorWindowVisible = true;
			var window = new ErrorWindow(message, details);
			window.Closed += (sender, args) => isErrorWindowVisible = false;
			window.Show();
		}
	}
}
