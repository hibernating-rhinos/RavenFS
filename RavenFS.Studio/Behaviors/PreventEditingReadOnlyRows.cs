using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interactivity;

namespace RavenFS.Studio.Behaviors
{
    public class PreventEditingReadOnlyRows : Behavior<DataGrid>
    {
        BindingEvaluator evaluator;

        public Binding IsReadOnlyBinding { get; set; }

        protected override void OnAttached()
        {
            base.OnAttached();

            AssociatedObject.BeginningEdit += HandleBeginningEdit;
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();

            AssociatedObject.BeginningEdit -= HandleBeginningEdit;
        }

        private void HandleBeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
	        if (IsReadOnlyBinding == null)
		        return;

	        evaluator = evaluator ?? (new BindingEvaluator());

            evaluator.DataContext = e.Row.DataContext;
            evaluator.SetBinding(BindingEvaluator.IsReadOnlyProperty, IsReadOnlyBinding);

	        if (evaluator.IsReadOnly)
		        e.Cancel = true;
        }

        private class BindingEvaluator : FrameworkElement
        {
            public static readonly DependencyProperty IsReadOnlyProperty =
                DependencyProperty.Register("IsReadOnly", typeof (bool), typeof (BindingEvaluator),
                                                           new PropertyMetadata(default(bool)));

            public bool IsReadOnly
            {
                get { return (bool) GetValue(IsReadOnlyProperty); }
                set { SetValue(IsReadOnlyProperty, value); }
            }
        }
    }
}