using System;
using System.Linq;

namespace RavenFS.Studio.Infrastructure
{
	public class ModelBase : NotifyPropertyChangedBase
	{
		public string GetQueryParam(string name)
		{
			string url = ApplicationModel.NavigationState;
			var indexOf = url.IndexOf('?');
			if (indexOf == -1)
				return null;

			var options = url.Substring(indexOf + 1).Split(new[] { '&', }, StringSplitOptions.RemoveEmptyEntries);

			return (from option in options
					where option.StartsWith(name) && option.Length > name.Length && option[name.Length] == '='
					select option.Substring(name.Length + 1))
					.FirstOrDefault();
		}
	}
}
