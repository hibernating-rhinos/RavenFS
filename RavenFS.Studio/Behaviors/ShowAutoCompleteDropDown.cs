using System.Windows.Controls;
using System.Windows.Interactivity;

namespace RavenFS.Studio.Behaviors
{
    public class ShowAutoCompleteDropDown : TriggerAction<AutoCompleteBox>
    {
        protected override void Invoke(object parameter)
        {
            AssociatedObject.IsDropDownOpen = true;
        }
    }
}
