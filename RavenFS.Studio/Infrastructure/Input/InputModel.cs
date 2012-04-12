using System;
using System.Collections;
using System.ComponentModel;

namespace RavenFS.Studio.Infrastructure.Input
{
	public class InputModel : NotifyPropertyChangedBase, INotifyDataErrorInfo
	{
        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

	    private bool hasAnswered;
		private string title;

        public InputModel()
        {
            answer = "";
        }

        public void SetDefaultAnswer(string answer)
        {
            this.answer = answer;
        }

	    public string Title
		{
			get { return title; }
			set
			{
				title = value;
				OnPropertyChanged();
			}
		}

		private string message;

		public string Message
		{
			get { return message; }
			set
			{
				message = value;
				OnPropertyChanged();
			}
		}

		private string answer;

		public string Answer
		{
			get { return answer; }
			set
			{
			    hasAnswered = true;
				answer = value;
				OnPropertyChanged();
                OnErrorsChanged(new DataErrorsChangedEventArgs("Answer"));
			}
		}

        public Func<string, string> ValidationCallback { get; set; }

	    private string GetError(string columnName)
	    {
	        return ValidationCallback != null ? ValidationCallback(Answer) : "";
	    }

        public bool EnsureValid()
        {
            if (HasErrors)
            {
                // user has pressed OK, which is effectively answering
                hasAnswered = true;
                OnErrorsChanged(new DataErrorsChangedEventArgs("Answer"));
                return false;
            }
            else
            {
                return true;
            }
        }

	    public IEnumerable GetErrors(string propertyName)
	    {
            // only report error when the user has actually entered something
	        if (hasAnswered)
	        {
	            var error = GetError(propertyName);
                if (!string.IsNullOrEmpty(error))
                {
                    yield return error;
                }
	        }
	    }

	    public bool HasErrors
	    {
            get { return !string.IsNullOrEmpty(GetError("Answer")); }
	    }

	    protected void OnErrorsChanged(DataErrorsChangedEventArgs e)
	    {
	        EventHandler<DataErrorsChangedEventArgs> handler = ErrorsChanged;
	        if (handler != null) handler(this, e);
	    }
	}
}