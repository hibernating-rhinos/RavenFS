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

namespace RavenFS.Studio.Infrastructure
{
	public class ModelResolver
	{
		public static readonly DependencyProperty AttachModelProperty =
			DependencyProperty.RegisterAttached("AttachModel", typeof(string), typeof(ModelResolver), new PropertyMetadata(null, AttachModelCallback));

		private static void AttachModelCallback(DependencyObject source, DependencyPropertyChangedEventArgs args)
		{
			var view = source as FrameworkElement;
			if (view == null)
				return;


			view.Loaded += (sender, eventArgs) =>
			{
				var modelType = Type.GetType(view.GetType().Namespace + ".Models." + args.NewValue);
				if (modelType == null)
					return;

				var model = Activator.CreateInstance(modelType);
				view.DataContext = model;
			};
		}

		public static string GetAttachModel(UIElement element)
		{
			return (string)element.GetValue(AttachModelProperty);
		}

		public static void SetAttachModel(UIElement element, string value)
		{
			element.SetValue(AttachModelProperty, value);
		}
	}
}