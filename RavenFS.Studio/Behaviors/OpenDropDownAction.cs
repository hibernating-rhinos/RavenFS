using System.Windows.Controls;
using System.Windows.Interactivity;

namespace RavenFS.Studio.Behaviors
{
    public class OpenDropDownAction : TriggerAction<AutoCompleteBox>
    {
        protected override void Invoke(object parameter)
        {
            Dispatcher.BeginInvoke(() => AssociatedObject.IsDropDownOpen = true);
        }
    }
}
