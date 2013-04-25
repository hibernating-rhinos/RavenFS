using System;
using System.Reactive;
using System.Reactive.Subjects;
using RavenFS.Studio.Behaviors;

namespace RavenFS.Studio.Infrastructure
{
	public abstract class ViewModel : NotifyPropertyChangedBase
	{
        public event EventHandler<UIMessageEventArgs> UIMessage;

	    private Subject<Unit> unloaded;

	    public void NotifyViewLoaded()
	    {
	        OnViewLoaded();
	    }

	    public void NotifyViewUnloaded()
	    {
		    if (unloaded != null)
			    unloaded.OnNext(Unit.Default);

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

	    protected void OnUIMessage(UIMessageEventArgs e)
	    {
	        var handler = UIMessage;
	        if (handler != null) handler(this, e);
	    }
	}
}
