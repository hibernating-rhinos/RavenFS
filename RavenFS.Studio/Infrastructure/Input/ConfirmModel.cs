using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

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

		private string question;
		public string Question
		{
			get { return question; }
			set
			{
				question = value;
				OnPropertyChanged();
			}
		}
	}
}