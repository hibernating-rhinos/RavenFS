using System.Windows;

namespace RavenFS.Studio.Behaviors
{
    public class ValuePropagator : DependencyObject
    {
        public static readonly DependencyProperty TargetProperty =
            DependencyProperty.Register("Target", typeof (DependencyObject), typeof (ValuePropagator), new PropertyMetadata(default(DependencyObject)));

        public static readonly DependencyProperty PropertyProperty =
            DependencyProperty.Register("Property", typeof (DependencyProperty), typeof (ValuePropagator), new PropertyMetadata(default(DependencyProperty)));

        public DependencyProperty Property
        {
            get { return (DependencyProperty) GetValue(PropertyProperty); }
            set { SetValue(PropertyProperty, value); }
        }

        public DependencyObject Target
        {
            get { return (DependencyObject) GetValue(TargetProperty); }
            set { SetValue(TargetProperty, value); }
        }

        public void Propagate(object value)
        {
	        if (Target == null || Property == null)
		        return;

	        Target.SetValue(Property, value);
        }
    }
}
