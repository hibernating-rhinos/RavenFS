using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

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
            if (IsActive)
            {
                VisualStateManager.GoToState(this, "Active", true);
            }
            else
            {
                VisualStateManager.GoToState(this, "Inactive", true);
            }

            if (IsFault)
            {
                VisualStateManager.GoToState(this, "Error", true);
            }
            else
            {
                VisualStateManager.GoToState(this, "Normal", true);
            }
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
