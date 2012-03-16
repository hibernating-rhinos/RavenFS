using System;
using System.Threading.Tasks;

namespace RavenFS.Studio.Infrastructure.Input
{
	public static class AskUser
	{
        public static Task AlertUser(string title, string message)
        {
            var dataContext = new ConfirmModel
			{
				Title = title,
				Message = message,
                AllowCancel = false,
			};
			var inputWindow = new ConfirmWindow()
			{
				DataContext = dataContext
			};

			var tcs = new TaskCompletionSource<bool>();

			inputWindow.Closed += (sender, args) =>
			{
				if (inputWindow.DialogResult == true)
					tcs.SetResult(true);
				else
					tcs.SetCanceled();
			};

			inputWindow.Show();

			return tcs.Task;
        }

		public static Task<string> QuestionAsync(string title, string question, Func<string,string> validator = null)
		{
			var dataContext = new InputModel
			{
				Title = title,
				Message = question,
                ValidationCallback = validator,
			};
			var inputWindow = new InputWindow
			{
				DataContext = dataContext
			};

			var tcs = new TaskCompletionSource<string>();

			inputWindow.Closed += (sender, args) =>
			{
				if (inputWindow.DialogResult == true)
					tcs.SetResult(dataContext.Answer);
				else
					tcs.SetCanceled();
			};

			inputWindow.Show();

			return tcs.Task;
		}

		public static Task<bool> ConfirmationAsync(string title, string question)
		{
			var dataContext = new ConfirmModel
			{
				Title = title,
				Message = question
			};
			var inputWindow = new ConfirmWindow
			{
				DataContext = dataContext
			};

			var tcs = new TaskCompletionSource<bool>();

			inputWindow.Closed += (sender, args) =>
			{
				if (inputWindow.DialogResult != null)
					tcs.SetResult(inputWindow.DialogResult.Value);
				else
					tcs.SetCanceled();
			};

			inputWindow.Show();

			return tcs.Task;
		}
	}
}