using System.Windows;
using System.Windows.Controls;

namespace RavenFS.Studio.Controls
{
    public class ActivityIndicator : Control
    {
        public ActivityIndicator()
        {
            this.DefaultStyleKey = typeof(ActivityIndicator);
            UpdateState();
        }

        public static readonly DependencyProperty IsActiveProperty =
            DependencyProperty.Register("IsActive", typeof(bool), typeof(ActivityIndicator), new PropertyMetadata(default(bool), HandlePropertyChanged));
        
        public static readonly DependencyProperty IsFaultProperty =
             DependencyProperty.Register("IsFault", typeof(bool), typeof(ActivityIndicator), new PropertyMetadata(default(bool), HandlePropertyChanged));

        private static void HandlePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as ActivityIndicator).UpdateState();
        }

        private void UpdateState()
        {
	        VisualStateManager.GoToState(this, IsActive ? "Active" : "Inactive", true);

	        VisualStateManager.GoToState(this, IsFault ? "Error" : "Normal", true);
        }

	    public bool IsFault
        {
            get { return (bool) GetValue(IsFaultProperty); }
            set { SetValue(IsFaultProperty, value); }
        }

        public bool IsActive
        {
            get { return (bool)GetValue(IsActiveProperty); }
            set { SetValue(IsActiveProperty, value); }
        }
    }
}
