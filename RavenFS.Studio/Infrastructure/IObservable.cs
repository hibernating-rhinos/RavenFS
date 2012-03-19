using System.ComponentModel;

namespace RavenFS.Studio.Infrastructure
{
	public interface IObservable : INotifyPropertyChanged
	{
		object Value { get; }
	}
}