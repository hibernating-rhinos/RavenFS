using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace RavenFS.Studio.Infrastructure
{
	public class PageView : Page
	{
	    private bool isLoaded;

		public PageView()
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
		    if (isLoaded)
			    NotifyModelLoaded();
	    }

	    private void OnUnloaded(object sender, RoutedEventArgs args)
	    {
		    if (Model != null)
			    Model.NotifyViewUnloaded();

		    isLoaded = false;
	    }

	    private void OnLoaded(object sender, RoutedEventArgs args)
	    {
	        isLoaded = true;
	        NotifyModelLoaded();
	    }

	    private void NotifyModelLoaded()
	    {
	        if (Model != null)
	        {
		        if (Model is PageModel)
			        (Model as PageModel).QueryParameters = NavigationContext.QueryString;

		        Model.NotifyViewLoaded();
	        }
	    }

	    protected ViewModel Model {get { return DataContext as ViewModel; }}
	}
}