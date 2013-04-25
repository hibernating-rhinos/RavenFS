using System;
using System.Windows.Markup;
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
