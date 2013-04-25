using System.Windows.Controls;
using System.Windows.Interactivity;

namespace RavenFS.Studio.Behaviors
{
    public class PlaceCursorAtEndOfTextAction : TargetedTriggerAction<TextBox>
    {
        protected override void Invoke(object parameter)
        {
            Target.SelectionLength = 0;
            Target.SelectionStart = Target.Text.Length;
        }
    }
}
