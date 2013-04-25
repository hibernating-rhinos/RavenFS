using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RavenFS.Util
{
    public class AwaitableQueue<T>
    {
        Queue<T> _queue = new Queue<T>();
        Queue<TaskCompletionSource<T>> _waitingTasks = new Queue<TaskCompletionSource<T>>(); 
        object _gate = new object();
        private bool _completed;


        public bool TryEnqueue(T item)
        {
            lock(_gate)
            {
                if (_completed)
                {
                    return false;
                }

                _queue.Enqueue(item);
            }

            FulfillWaitingTasks();
            return true;
        }

        public bool TryDequeue(out T item)
        {
            lock(_gate)
            {
                if (_queue.Count > 0)
                {
                    item = _queue.Dequeue();
                    return true;
                }
	            
				item = default(T);
	            return false;
            }
        }

        public Task<T> DequeueOrWaitAsync()
        {
            bool wasEnqueued = false;
            var tcs = new TaskCompletionSource<T>();
            lock(_gate)
            {
                if (_completed)
                {
                    tcs.SetCanceled();
                }
                else
                {
                    _waitingTasks.Enqueue(tcs);
                    wasEnqueued = true;
                }
            }

	        if (wasEnqueued)
		        FulfillWaitingTasks();

	        return tcs.Task;
        }

        public void SignalCompletion()
        {
            var tasksToCancel = new List<TaskCompletionSource<T>>();

            lock(_gate)
            {
                _completed = true;
                while (_waitingTasks.Count > 0)
                {
                    tasksToCancel.Add(_waitingTasks.Dequeue());
                }
            }

            foreach (var taskCompletionSource in tasksToCancel)
            {
                taskCompletionSource.TrySetCanceled();
            }
        }

        private void FulfillWaitingTasks()
        {
            for (var pair = GetNextItemAndTask(); pair.Item2 != null; pair = GetNextItemAndTask())
            {
                pair.Item2.TrySetResult(pair.Item1);
            }
        }

        private Tuple<T,TaskCompletionSource<T>> GetNextItemAndTask()
        {
            lock(_gate)
            {
	            if (_queue.Count > 0 && _waitingTasks.Count > 0)
		            return Tuple.Create(_queue.Dequeue(), _waitingTasks.Dequeue());

	            return Tuple.Create(default(T), default(TaskCompletionSource<T>));
            }
        }
    }
}