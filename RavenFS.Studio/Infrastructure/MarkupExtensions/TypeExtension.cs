using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Xaml;

namespace RavenFS.Studio.Infrastructure.MarkupExtensions
{
    public class TypeExtension : IMarkupExtension<Type>
    {
        public string Name { get; set; }

        public TypeExtension()
        {
        }

        public Type ProvideValue(IServiceProvider serviceProvider)
        {
            var resolver = serviceProvider.GetService(typeof(IXamlTypeResolver)) as IXamlTypeResolver;
            var type = resolver.Resolve(Name);

            return type;
        }
    }
}
