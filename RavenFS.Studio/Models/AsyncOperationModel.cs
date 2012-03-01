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
using RavenFS.Studio.Infrastructure;

namespace RavenFS.Studio.Models
{
    public class AsyncOperationModel : Model
    {
        string name;
        int progress;
        string error;
        AsyncOperationStatus status;

        public AsyncOperationModel()
        {
            Status = AsyncOperationStatus.Queued;
        }

        public string Name
        {
            get { return name; }
            set
            {
                name = value;
                OnPropertyChanged("Name");
            }
        }

        public int Progress
        {
            get { return progress; }
            private set
            {
                progress = value;
                OnPropertyChanged("Progress");
            }
        }

        public AsyncOperationStatus Status
        {
            get { return status; }
            private set
            {
                status = value;
                OnPropertyChanged("Status");
            }
        }

        public string Error
        {
            get { return error; }
            private set
            {
                error = value;
                OnPropertyChanged("Error");
            }
        }

        public void ProgressChanged(double amountCompleted, double amountToDo)
        {
            ProgressChanged((int)((amountCompleted / amountToDo) * 100));
        }

        public void ProgressChanged(int progress)
        {
            if (Status == AsyncOperationStatus.Queued)
            {
                Status = AsyncOperationStatus.Processing;
            }

            Progress = progress;
        }

        public void Completed()
        {
            Status = AsyncOperationStatus.Completed;
        }

        public void Faulted(Exception exception)
        {
            Status = AsyncOperationStatus.Error;
            if (exception != null)
            {
                Error = exception.Message;
            }
        }
    }
}
