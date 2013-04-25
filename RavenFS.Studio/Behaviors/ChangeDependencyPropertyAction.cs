using System.Windows;
using System.Windows.Interactivity;

namespace RavenFS.Studio.Behaviors
{
    public class ChangeDependencyPropertyAction : TargetedTriggerAction<DependencyObject>
    {
        public static readonly DependencyProperty PropertyProperty =
            DependencyProperty.Register("Property", typeof (DependencyProperty), typeof (ChangeDependencyPropertyAction), new PropertyMetadata(default(DependencyProperty)));

        public DependencyProperty Property
        {
            get { return (DependencyProperty) GetValue(PropertyProperty); }
            set { SetValue(PropertyProperty, value); }
        }

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof (object), typeof (ChangeDependencyPropertyAction), new PropertyMetadata(default(object)));

        public object Value
        {
            get { return GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        protected override void Invoke(object parameter)
        {
	        if (Target != null && Property != null)
		        Target.SetValue(Property, Value);
        }
    }
}
