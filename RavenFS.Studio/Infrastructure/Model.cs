using System;
using System.Reactive;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace RavenFS.Studio.Infrastructure
{
	public class Model : NotifyPropertyChangedBase
	{
		private Task currentTask;
		private DateTime lastRefresh;
		protected TimeSpan RefreshRate { get; set; }
	    protected static Task _completedTask;
	    private Subject<Unit> unloaded;

		protected Model()
		{
			RefreshRate = TimeSpan.FromSeconds(5);
		}

	    protected static Task Completed
	    {
	        get
	        {
	            if (_completedTask == null)
	            {
	                var tcs = new TaskCompletionSource<bool>();
	                tcs.SetResult(true);
	                _completedTask = tcs.Task;
	            }

	            return _completedTask;
	        }
	    }

		internal void ForceTimerTicked()
		{
			lastRefresh = DateTime.MinValue;
			TimerTicked();
		}

		internal void TimerTicked()
		{
			if (currentTask != null)
				return;

			lock (this)
			{
				if (currentTask != null)
					return;

				if (DateTime.Now - lastRefresh < GetRefreshRate())
					return;

				currentTask = TimerTickedAsync();

				if (currentTask == null)
					return;

				currentTask
					.Catch()
					.Finally(() =>
					{
						lastRefresh = DateTime.Now;
						currentTask = null;
					});
			}
		}

		private TimeSpan GetRefreshRate()
		{
			//if (Debugger.IsAttached)
			//    return RefreshRate.Add(TimeSpan.FromMinutes(5));
			return RefreshRate;
		}

		protected virtual Task TimerTickedAsync()
		{
			return null;
		}

	    public void NotifyViewLoaded()
	    {
	        OnViewLoaded();
	    }

	    public void NotifyViewUnloaded()
	    {
            if (unloaded != null)
            {
                unloaded.OnNext(Unit.Default);
            }

	        OnViewUnloaded();
	    }

	    protected virtual void OnViewLoaded()
	    {
            
	    }

	    protected virtual void OnViewUnloaded()
	    {

	    }

	    protected IObservable<Unit> Unloaded
	    {
	        get { return unloaded ?? (unloaded = new Subject<Unit>()); }
	    } 
	}
}
