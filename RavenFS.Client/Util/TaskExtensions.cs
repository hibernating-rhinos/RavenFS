using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RavenFS.Client.Util
{
    public static class TaskExtensions
    {
        public static void AssertNotFailed(this Task task)
        {
            if (task.IsFaulted)
                task.Wait(); // would throw
        }

        public static Task<T> FromException<T>(Exception ex)
        {
            var taskCompletionSource = new TaskCompletionSource<T>();
            taskCompletionSource.SetException(ex);
            return taskCompletionSource.Task;
        }
    }
}
