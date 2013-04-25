using System;
using System.Windows;
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
		        return;

	        InvokeActions(null);
        }
    }
}
