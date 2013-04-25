using System.Windows;
using System.Windows.Controls;

namespace RavenFS.Studio.Infrastructure
{
    public class DialogView : ChildWindow
    {
        public DialogView()
        {
            DataContextChanged += HandleDataContextChanged;
            Loaded += HandleLoaded;
            Unloaded += HandleUnloaded;
        }

        private void HandleUnloaded(object sender, RoutedEventArgs e)
        {
	        if (HasModel)
		        Model.NotifyViewUnloaded();
        }

        private void HandleLoaded(object sender, RoutedEventArgs e)
        {
	        if (HasModel)
		        Model.NotifyViewLoaded();
        }

        private void HandleDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var oldModel = e.OldValue as DialogModel;
	        if (oldModel != null)
		        oldModel.CloseRequested -= HandleCloseRequested;

	        var newModel = e.NewValue as DialogModel;
	        if (newModel != null)
		        newModel.CloseRequested += HandleCloseRequested;
        }

        private void HandleCloseRequested(object sender, CloseRequestedEventArgs e)
        {
            DialogResult = e.DialogResult;
        }

        public DialogModel Model
        {
            get { return DataContext as DialogModel; }
            set { DataContext = value; }
        }

        protected bool HasModel
        {
            get { return Model != null; }
        }
    }
}
