using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace RavenFS.Client
{
	/// <summary>
	///     Extension methods to handle common scenarios
	/// </summary>
	public static class ExceptionExtensions
	{
		public static Task TryThrowBetterError(this Task self)
		{
			return self.ContinueWith(task =>
				                         {
					                         if (task.Status != TaskStatus.Faulted)
						                         return task;

					                         var webException = task.Exception.ExtractSingleInnerException() as WebException;
					                         if (webException == null || webException.Response == null)
						                         return task;

					                         throw webException.BetterWebExceptionError();
				                         })
			           .Unwrap();
		}

		public static Exception BetterWebExceptionError(this WebException webException)
		{
			var httpWebResponse = webException.Response as HttpWebResponse;
			if (httpWebResponse != null)
			{
				if (httpWebResponse.StatusCode == HttpStatusCode.PreconditionFailed)
				{
					using (var stream = webException.Response.GetResponseStream())
					{
						return new JsonSerializer().Deserialize<SynchronizationException>(new JsonTextReader(new StreamReader(stream)));
					}
				}
				else if (httpWebResponse.StatusCode == HttpStatusCode.MethodNotAllowed)
				{
					using (var stream = webException.Response.GetResponseStream())
					{
						return new JsonSerializer().Deserialize<ConcurrencyException>(new JsonTextReader(new StreamReader(stream)));
					}
				}
				else if (httpWebResponse.StatusCode == HttpStatusCode.NotFound)
				{
					return new FileNotFoundException();
				}
			}

			using (var stream = webException.Response.GetResponseStream())
			using (var reader = new StreamReader(stream))
			{
				var readToEnd = reader.ReadToEnd();
				return new InvalidOperationException(
					webException + Environment.NewLine + readToEnd);
			}
		}

		public static Task<T> TryThrowBetterError<T>(this Task<T> self)
		{
			return self.ContinueWith(task =>
				                         {
					                         if (task.Status != TaskStatus.Faulted)
						                         return task;

					                         var webException = task.Exception.ExtractSingleInnerException() as WebException;
					                         if (webException == null || webException.Response == null)
						                         return task;

					                         throw webException.BetterWebExceptionError();
				                         })
			           .Unwrap();
		}

		public static Exception TryThrowBetterError(this Exception exception)
		{
			var aggregateException = exception as AggregateException;
			if (aggregateException == null)
			{
				var web = exception as WebException;
				return web != null ? web.BetterWebExceptionError() : exception;
			}
			var webException = aggregateException.ExtractSingleInnerException() as WebException;
			if (webException == null || webException.Response == null)
				return aggregateException;

			return webException.BetterWebExceptionError();
		}

		/// <summary>
		///     Recursively examines the inner exceptions of an <see cref="AggregateException" /> and returns a single child exception.
		/// </summary>
		/// <returns>
		///     If any of the aggregated exceptions have more than one inner exception, null is returned.
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
		///     Extracts a portion of an exception for a user friendly display
		/// </summary>
		/// <param name="e">The exception.</param>
		/// <returns>The primary portion of the exception message.</returns>
		public static string SimplifyError(this Exception e)
		{
			var parts = e.Message.Split(new[] {"\r\n   "}, StringSplitOptions.None);
			var firstLine = parts.First();
			var index = firstLine.IndexOf(':');
			return index > 0
				       ? firstLine.Remove(0, index + 2)
				       : firstLine;
		}
	}
}