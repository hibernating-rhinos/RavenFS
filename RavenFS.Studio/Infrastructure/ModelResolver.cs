using System;
using System.Windows;

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
			    var typeName = "RavenFS.Studio.Models." + args.NewValue;
			    var modelType = Type.GetType(typeName);
				if (modelType == null)
					throw new ArgumentException(string.Format("Model type {0} was not found.", typeName));

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