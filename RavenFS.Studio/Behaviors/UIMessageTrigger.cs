using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using EventTrigger = System.Windows.Interactivity.EventTrigger;

namespace RavenFS.Studio.Behaviors
{
    public class UIMessageTrigger : EventTrigger
    {
        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register("Message", typeof (string), typeof (UIMessageTrigger), new PropertyMetadata(default(string)));

        public string Message
        {
            get { return (string) GetValue(MessageProperty); }
            set { SetValue(MessageProperty, value); }
        }

        protected override string GetEventName()
        {
            return "UIMessage";
        }

        protected override void OnEvent(EventArgs eventArgs)
        {
            var messageEventArgs = eventArgs as UIMessageEventArgs;
            if (messageEventArgs == null || !string.Equals(messageEventArgs.Message, Message))
            {
                return;
            }

            InvokeActions(null);
        }
    }
}
