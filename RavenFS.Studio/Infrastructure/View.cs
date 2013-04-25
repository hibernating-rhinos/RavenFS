using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;

namespace RavenFS.Studio.Infrastructure
{
    public class View : UserControl
    {
         private bool isLoaded;

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
			    Model.NotifyViewLoaded();
	    }

	    protected ViewModel Model {get { return DataContext as ViewModel; }}
    }
}
