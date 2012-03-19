using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

namespace RavenFS.Studio.Infrastructure
{
	public class View : Page
	{
		public static List<View> CurrentViews { get; set; }

		private static readonly DispatcherTimer dispatcherTimer;

	    private bool isLoaded;

		static View()
		{
			CurrentViews = new List<View>();

            if (!DesignerProperties.IsInDesignTool)
            {
                dispatcherTimer = new DispatcherTimer
                                      {
                                          Interval = TimeSpan.FromSeconds(1),
                                      };
                dispatcherTimer.Tick += DispatcherTimerOnTick;
                dispatcherTimer.Start();
            }
		}

		private static void DispatcherTimerOnTick(object sender, EventArgs eventArgs)
		{
			foreach (var ctx in CurrentViews.Select(view => view.DataContext).Distinct())
			{
				InvokeTimerTicked(ctx);
			}
		}

		public static void UpdateAllFromServer()
		{
			foreach (var ctx in CurrentViews.Select(view => view.DataContext).Distinct())
			{
				InvokeOnModel(ctx, model => model.ForceTimerTicked());
			}
		}

		private static void InvokeTimerTicked(object ctx)
		{
			InvokeOnModel(ctx, model => model.TimerTicked());
		}

		private static void InvokeOnModel(object ctx, Action<Model> action)
		{
			var model = ctx as Model;
			if (model == null)
			{
				var observable = ctx as IObservable;
				if (observable == null)
					return;
				model = observable.Value as Model; 
				if (model == null)
				{
					PropertyChangedEventHandler observableOnPropertyChanged = null;
					observableOnPropertyChanged = (sender, args) =>
					{
						if (args.PropertyName != "Value")
							return;
						observable.PropertyChanged -= observableOnPropertyChanged;
						InvokeOnModel(ctx, action);
					};
					observable.PropertyChanged += observableOnPropertyChanged;
				}
			}
			action(model);
		}

		public View()
		{
            if (!DesignerProperties.IsInDesignTool)
            {
                Loaded += OnLoaded;
                DataContextChanged += OnDataContextChanged;
                Unloaded += OnUnloaded;
            }
		}

	    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs args)
	    {
	        InvokeTimerTicked(args.NewValue);
            if (isLoaded)
            {
                NotifyModelLoaded();
            }
	    }

	    private void OnUnloaded(object sender, RoutedEventArgs args)
	    {
	        CurrentViews.Remove(this);
            if (Model != null)
            {
                Model.NotifyViewUnloaded();
            }

            isLoaded = false;
	    }

	    private void OnLoaded(object sender, RoutedEventArgs args)
	    {
	        isLoaded = true;
	        CurrentViews.Add(this);
	        NotifyModelLoaded();
	    }

	    private void NotifyModelLoaded()
	    {
	        if (Model != null)
	        {
	            if (Model is PageModel)
	            {
	                (Model as PageModel).QueryParameters = NavigationContext.QueryString;
	            }

	            Model.NotifyViewLoaded();
	        }
	    }

	    protected Model Model {get { return DataContext as Model; }}
	}
}