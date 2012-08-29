using System;
using System.Threading.Tasks;

namespace RavenFS.Client.Changes
{
	public interface IObservableWithTask<out T> : IObservable<T>
	{
		Task Task { get; }
	}
}