using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace RavenFS.Client
{
	///<summary>
	/// Extension methods to handle common scenarios
	///</summary>
	public static class ExceptionExtensions
	{
		public static Task TryThrowBetteError(this Task self)
		{
			return self.ContinueWith(task =>
			{
				if (task.Status != TaskStatus.Faulted)
					return task;

				var webException = task.Exception.ExtractSingleInnerException() as WebException;
				if (webException == null || webException.Response == null)
					return task;

				using (var stream = webException.Response.GetResponseStream())
				using (var reader = new StreamReader(stream))
				{
					throw new InvalidOperationException(reader.ReadToEnd());
				}
			})
			.Unwrap();
		}

		public static Task<T> TryThrowBetteError<T>(this Task<T> self)
		{
			return self.ContinueWith(task =>
			{
				if (task.Status != TaskStatus.Faulted)
					return task;

				var webException = task.Exception.ExtractSingleInnerException() as WebException;
				if (webException == null || webException.Response == null)
					return task;

				using (var stream = webException.Response.GetResponseStream())
				using (var reader = new StreamReader(stream))
				{
					throw new InvalidOperationException(reader.ReadToEnd());
				}
			})
			.Unwrap();
		}

		/// <summary>
		/// Recursively examines the inner exceptions of an <see cref="AggregateException"/> and returns a single child exception.
		/// </summary>
		/// <returns>
		/// If any of the aggregated exceptions have more than one inner exception, null is returned.
		/// </returns>
		public static Exception ExtractSingleInnerException(this AggregateException e)
		{
			if (e == null)
				return null;
			while (true)
			{
				if (e.InnerExceptions.Count != 1)
					return null;

				var aggregateException = e.InnerExceptions[0] as AggregateException;
				if (aggregateException == null)
					break;
				e = aggregateException;
			}

			return e.InnerExceptions[0];
		}

		/// <summary>
		/// Extracts a portion of an exception for a user friendly display
		/// </summary>
		/// <param name="e">The exception.</param>
		/// <returns>The primary portion of the exception message.</returns>
		public static string SimplifyError(this Exception e)
		{
			var parts = e.Message.Split(new[] {  "\r\n   " }, StringSplitOptions.None);
			var firstLine = parts.First();
			var index = firstLine.IndexOf(':');
			return index > 0
				? firstLine.Remove(0,index + 2)
				: firstLine;
		}
	}
}
