using System;

namespace RavenFS.Util
{
	public class DispsableAction : IDisposable
	{
		private readonly Action action;

		public DispsableAction(Action action)
		{
			this.action = action;
		}

		public void Dispose()
		{
			action();
		}
	}
}