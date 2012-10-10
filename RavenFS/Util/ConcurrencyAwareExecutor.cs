namespace RavenFS.Util
{
	using System;
	using System.Threading;
	using Client;

	public static class ConcurrencyAwareExecutor
	{
		public static void Execute(Action action, Func<ConcurrencyException, Exception> failed = null, int retries = 50)
		{
			var shouldRetry = false;

			do
			{
				try
				{
					action();
					shouldRetry = false;
				}
				catch (ConcurrencyException ce)
				{
					if (retries-- > 0)
					{
						shouldRetry = true;
						Thread.Sleep(50);
						continue;
					}

					if (failed != null)
					{
						throw failed(ce);
					}

					throw;
				}
			} while (shouldRetry);
		}
	}
}