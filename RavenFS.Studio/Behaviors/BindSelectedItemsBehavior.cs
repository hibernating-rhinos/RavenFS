using System;
using System.Collections;
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
using RavenFS.Studio.Infrastructure;

namespace RavenFS.Studio.Behaviors
{
    public class BindSelectedItemsBehavior : Behavior<DataGrid>
    {
        private Func<IEnumerable> snapshotProvider;

        public static readonly DependencyProperty TargetProperty =
            DependencyProperty.Register("Target", typeof(ItemSelection), typeof(BindSelectedItemsBehavior), new PropertyMetadata(default(ItemSelection), HandleItemSelectionChanged));

        public BindSelectedItemsBehavior()
        {
            snapshotProvider = () => AssociatedObject.SelectedItems;
        }

        protected override void OnAttached()
        {
            AssociatedObject.SelectionChanged += HandleSelectionChanged;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.SelectionChanged -= HandleSelectionChanged;
        }

        private void HandleSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Target != null)
            {
                Target.NotifySelectionChanged(AssociatedObject.SelectedItems.Count, snapshotProvider);
            }
        }

        public ItemSelection Target
        {
            get { return (ItemSelection)GetValue(TargetProperty); }
            set { SetValue(TargetProperty, value); }
        }

        private static void HandleItemSelectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var behavior = d as BindSelectedItemsBehavior;

            if (e.OldValue != null)
            {
                (e.OldValue as ItemSelection).DesiredSelectionChanged -= behavior.HandleDesiredSelectionChanged;
            }

            if (e.NewValue != null)
            {
                (e.NewValue as ItemSelection).DesiredSelectionChanged += behavior.HandleDesiredSelectionChanged;
            }
        }


        private void HandleDesiredSelectionChanged(object sender, DesiredSelectionChangedEventArgs e)
        {
            AssociatedObject.SelectedItems.Clear();
            foreach (var item in e.Items)
            {
                AssociatedObject.SelectedItems.Add(item);
            }
        }
    }
}
