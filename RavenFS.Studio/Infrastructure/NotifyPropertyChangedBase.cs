using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace RavenFS.Studio.Infrastructure
{
	public class NotifyPropertyChangedBase : INotifyPropertyChanged
	{
		private event PropertyChangedEventHandler PropertyChangedInternal;
		public event PropertyChangedEventHandler PropertyChanged
		{
			add
			{
				var state = new EventState(value);
				PropertyChangedInternal += state.Invoke;
			}
			remove
			{
				EventState firstOrDefault = PropertyChangedInternal.GetInvocationList()
					.Select(x => ((EventState)x.Target))
					.FirstOrDefault(x => x.Value == value);

				if (firstOrDefault == null)
					return;

				PropertyChangedInternal -= firstOrDefault.Invoke;
			}
		}

		private class EventState
		{
			public PropertyChangedEventHandler Value { get; private set; }
			private readonly Dispatcher dispatcher = Deployment.Current.Dispatcher;

			public EventState(PropertyChangedEventHandler value)
			{
				this.Value = value;
			}

			public void Invoke(object sender, PropertyChangedEventArgs e)
			{
				if (dispatcher.CheckAccess())
					Value(sender, e);
				else
					dispatcher.InvokeAsync(() => Value(sender, e));
			}
		}

		protected void OnEverythingChanged()
		{
			var handler = PropertyChangedInternal;
			if (handler == null)
				return;

			handler(this, new PropertyChangedEventArgs(""));
		}

		protected void OnPropertyChanged()
		{
			var stackTrace = new StackTrace();
			var name = stackTrace.GetFrame(1).GetMethod().Name.Substring(4);

			OnPropertyChanged(name);
		}

		protected void OnPropertyChanged(string name)
		{
			var handler = PropertyChangedInternal;
			if (handler == null)
				return;

			handler(this, new PropertyChangedEventArgs(name));
		}
	}
}