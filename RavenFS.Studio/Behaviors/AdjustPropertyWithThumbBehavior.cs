using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interactivity;
using RavenFS.Studio.Extensions;

namespace RavenFS.Studio.Behaviors
{
    public class AdjustPropertyWithThumbBehavior : Behavior<Thumb>
    {
        public static readonly DependencyProperty TargetProperty =
            DependencyProperty.Register("Target", typeof (DependencyObject), typeof (AdjustPropertyWithThumbBehavior), new PropertyMetadata(default(DependencyObject)));

        public static readonly DependencyProperty VerticalPropertyProperty =
            DependencyProperty.Register("VerticalProperty", typeof(object), typeof(AdjustPropertyWithThumbBehavior), new PropertyMetadata(default(DependencyProperty)));

        public static readonly DependencyProperty VerticalMinProperty =
            DependencyProperty.Register("VerticalMin", typeof (double), typeof (AdjustPropertyWithThumbBehavior), new PropertyMetadata(0.0));

        public static readonly DependencyProperty VerticalMaxProperty =
            DependencyProperty.Register("VerticalMax", typeof (double), typeof (AdjustPropertyWithThumbBehavior), new PropertyMetadata(double.MaxValue));

        public static readonly DependencyProperty PropagateVerticalValuesToProperty =
            DependencyProperty.Register("PropagateVerticalValuesTo", typeof (ValuePropagatorCollection), typeof (AdjustPropertyWithThumbBehavior), new PropertyMetadata(default(ValuePropagatorCollection)));

        public AdjustPropertyWithThumbBehavior()
        {
            SetValue(PropagateVerticalValuesToProperty, new ValuePropagatorCollection());
        }

        public ValuePropagatorCollection PropagateVerticalValuesTo
        {
            get { return (ValuePropagatorCollection) GetValue(PropagateVerticalValuesToProperty); }
            set { SetValue(PropagateVerticalValuesToProperty, value); }
        }

        public double VerticalMax
        {
            get { return (double) GetValue(VerticalMaxProperty); }
            set { SetValue(VerticalMaxProperty, value); }
        }

        public double VerticalMin
        {
            get { return (double) GetValue(VerticalMinProperty); }
            set { SetValue(VerticalMinProperty, value); }
        }

        public DependencyProperty VerticalProperty
        {
            get { return (DependencyProperty)GetValue(VerticalPropertyProperty); }
            set { SetValue(VerticalPropertyProperty, value); }
        }

        public DependencyObject Target
        {
            get { return (DependencyObject) GetValue(TargetProperty); }
            set { SetValue(TargetProperty, value); }
        }

        protected override void OnAttached()
        {
            base.OnAttached();

            AssociatedObject.DragDelta += HandleThumbDragged;
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();

            AssociatedObject.DragDelta -= HandleThumbDragged;
        }



        private void HandleThumbDragged(object sender, DragDeltaEventArgs e)
        {
            if (Target == null || VerticalProperty == null)
            {
                return;
            }

            var value = Convert.ToDouble(Target.GetValue(VerticalProperty));
            value -= e.VerticalChange;
            value = value.Clamp(VerticalMin, VerticalMax);

            Target.SetValue(VerticalProperty, value);

            if (PropagateVerticalValuesTo != null)
            {
                foreach (var propagator in PropagateVerticalValuesTo)
                {
                    propagator.Propagate(value);
                }
            }
        }
    }
}