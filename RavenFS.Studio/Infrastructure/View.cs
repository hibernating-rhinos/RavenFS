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
                Loaded += (sender, args) => CurrentViews.Add(this);
                DataContextChanged += (sender, args) => InvokeTimerTicked(args.NewValue);
                Unloaded += (sender, args) => CurrentViews.Remove(this);
            }
		}
	}
}