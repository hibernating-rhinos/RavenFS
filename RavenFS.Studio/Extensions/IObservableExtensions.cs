using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Net;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace RavenFS.Studio.Extensions
{
    public static class IObservableExtensions
    {
        public static IDisposable SubscribeWeakly<T, TTarget>(this IObservable<T> observable, TTarget target, Action<TTarget,T> onNext) where TTarget : class
        {
            IDisposable subscription = null;
            var reference = new WeakReference(target);

            AssertOnNextIsStatic(onNext);

            subscription = observable.Subscribe(item =>
                                                    {
                                                        var currentTarget = (TTarget) reference.Target;
                                                        if (currentTarget != null)
                                                        {
                                                            onNext(currentTarget, item);
                                                        }
                                                        else
                                                        {
                                                            subscription.Dispose();
                                                        }
                                                    });

            return subscription;
        }

        [Conditional("DEBUG")]
        private static void AssertOnNextIsStatic<T, TTarget>(Action<TTarget, T> onNext)
        {
            Debug.Assert(onNext.Method.IsStatic, "Only static methods must be used with SubscribeWeakly");
        }

        public static IObservable<NotifyCollectionChangedEventArgs> ObserveCollectionChanged(this INotifyCollectionChanged collection)
        {
            return Observable.FromEvent<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
                handler => (s, e) => handler(e), handler => collection.CollectionChanged += handler,
                handler => collection.CollectionChanged -= handler);
        } 
    }
}
