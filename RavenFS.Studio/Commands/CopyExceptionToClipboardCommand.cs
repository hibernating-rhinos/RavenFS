using System;
using System.Windows;
using RavenFS.Studio.Infrastructure;

namespace RavenFS.Studio.Commands
{
    public class CopyExceptionToClipboardCommand : Command
    {
        public override bool CanExecute(object parameter)
        {
            return parameter is Exception;
        }

        public override void Execute(object parameter)
        {
            var exception = parameter as Exception;
	        if (exception == null)
		        return;

	        var text = exception.ToString();
            Clipboard.SetText(text);
        }

    }
}
