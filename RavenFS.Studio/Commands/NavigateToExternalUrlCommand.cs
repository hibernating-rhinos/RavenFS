using System;
using System.Net;
using System.Windows;
using System.Windows.Browser;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using RavenFS.Studio.Infrastructure;
using RavenFS.Studio.Models;

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
