using System.Windows.Controls;
using System.Windows.Interactivity;

namespace RavenFS.Studio.Behaviors
{
    public class FocusElementAction : TargetedTriggerAction<Control>
    {
        protected override void Invoke(object parameter)
        {
            Target.Focus();
        }
    }
}
