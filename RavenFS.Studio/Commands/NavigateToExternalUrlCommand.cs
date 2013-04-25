using System;
using System.Windows.Browser;
using RavenFS.Studio.Infrastructure;

namespace RavenFS.Studio.Commands
{
    public class NavigateToExternalUrlCommand : Command
    {
        public override bool CanExecute(object parameter)
        {
            var href = parameter as string;
            return href != null;
        }

        public override void Execute(object parameter)
        {
            var href = parameter as string;
            HtmlPage.Window.Navigate(new Uri(href, UriKind.Absolute), "_blank");
        }
    }
}
