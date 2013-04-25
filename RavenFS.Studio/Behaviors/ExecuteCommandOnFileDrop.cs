using System.Windows;
using System.Windows.Input;
using System.Windows.Interactivity;

namespace RavenFS.Studio.Behaviors
{
    public class ExecuteCommandOnFileDrop : Behavior<FrameworkElement>
    {
        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.Register("Command", typeof (ICommand), typeof (ExecuteCommandOnFileDrop), new PropertyMetadata(default(ICommand)));

        public ICommand Command
        {
            get { return (ICommand) GetValue(CommandProperty); }
            set { SetValue(CommandProperty, value); }
        }

        protected override void OnAttached()
        {
            AssociatedObject.AllowDrop = true;
            AssociatedObject.Drop += HandleDrop;
        }

        private void HandleDrop(object sender, DragEventArgs e)
        {
	        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
		        return;

	        if (Command != null)
		        Command.Execute(e.Data.GetData(DataFormats.FileDrop));
        }
    }
}