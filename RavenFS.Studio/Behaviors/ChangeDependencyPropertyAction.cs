using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Interactivity;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

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
            get { return (object) GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        protected override void Invoke(object parameter)
        {
            if (Target != null && Property != null)
            {
                Target.SetValue(Property, Value);
            }
        }
    }
}
