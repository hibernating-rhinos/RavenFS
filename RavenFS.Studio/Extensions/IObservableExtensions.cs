using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using RavenFS.Studio.Infrastructure;

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

        public static IObservable<EventPattern<PropertyChangedEventArgs>> ObservePropertyChanged(this INotifyPropertyChanged source, string propertyName)
        {
            return Observable.FromEventPattern<PropertyChangedEventHandler, PropertyChangedEventArgs>(
                handler => (s, e) => handler(s, e),
                handler => source.PropertyChanged += handler,
                handler => source.PropertyChanged -= handler);
        } 

        public static IObservable<T> ObserveChanged<T>(this Observable<T> observable)
        {
            return observable.ObservePropertyChanged("Value").Select(_ => observable.Value);
        }

        /// <summary>
        /// Samples an event stream such that the very first event is reported, but then no further events
        /// are reported until a Timespan of delay has elapsed, at which point the most recent event will be reported. And so on.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="delay"></param>
        /// <returns></returns>
        public static IObservable<T> SampleResponsive<T>(this IObservable<T> source, TimeSpan delay)
        {
            // code from http://stackoverflow.com/questions/3211134/how-to-throttle-event-stream-using-rx/3224723#3224723
            return source.Publish(src =>
            {
                var fire = new Subject<T>();

                var whenCanFire = fire
                    .Select(u => new Unit())
                    .Delay(delay)
                    .StartWith(new Unit());

                var subscription = src
                    .CombineVeryLatest(whenCanFire, (x, flag) => x)
                    .Subscribe(fire);

                return fire.Finally(subscription.Dispose);
            });
        }

        public static IObservable<TResult> CombineVeryLatest<TLeft, TRight, TResult>(this IObservable<TLeft> leftSource, IObservable<TRight> rightSource, Func<TLeft, TRight, TResult> selector)
        {
            // code from http://stackoverflow.com/questions/3211134/how-to-throttle-event-stream-using-rx/3224723#3224723
            return Observable.Defer(() =>
            {
                int l = -1, r = -1; // the last yielded index from each sequence
                return Observable.CombineLatest(
                    leftSource.Select(Tuple.Create<TLeft, int>), // create a tuple which marks each item in a sequence with its index
                    rightSource.Select(Tuple.Create<TRight, int>), (x, y) => new { x, y })
                    .Where(t => t.x.Item2 != l && t.y.Item2 != r) // don't yield a pair if the left or right has already been yielded
                    .Do(t => { l = t.x.Item2; r = t.y.Item2; }) // record the index of the last item yielded from each sequence
                    .Select(t => selector(t.x.Item1, t.y.Item1));
            });
        }
    }
}
