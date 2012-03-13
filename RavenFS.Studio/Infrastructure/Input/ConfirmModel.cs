namespace RavenFS.Studio.Infrastructure.Input
{
	public class ConfirmModel : NotifyPropertyChangedBase
	{
		private string title;
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

        bool allowCancel = true;

        public bool AllowCancel
        {
            get { return allowCancel; }
            set
            {
                allowCancel = value;
                OnPropertyChanged();
            }
        }
	}
}