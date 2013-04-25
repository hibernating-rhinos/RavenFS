using System.Windows.Controls.Primitives;
using System.Windows.Interactivity;

namespace RavenFS.Studio.Behaviors
{
    public class OpenPopupAction : TargetedTriggerAction<Popup>
    {
        protected override void Invoke(object parameter)
        {
            Target.IsOpen = true;
        }
    }
}
