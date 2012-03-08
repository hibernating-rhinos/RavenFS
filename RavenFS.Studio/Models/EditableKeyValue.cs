using System;
using System.Collections;
using System.ComponentModel;
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
    public class EditableKeyValue : NotifyPropertyChangedBase, INotifyDataErrorInfo
    {
        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

        string key;
        string value;
        private bool isReadOnly;

        public string Key
        {
            get { return key; }
            set
            {
                key = value;
                OnPropertyChanged("Key");
            }
        }

        public string Value
        {
            get { return value; }
            set
            {
                this.value = value;
                OnPropertyChanged("Value");
            }
        }

        public IEnumerable GetErrors(string propertyName)
        {
            yield break;
        }

        public bool HasErrors
        {
            get { return false; }
        }

        public bool IsReadOnly
        {
            get 
            {
                return isReadOnly;
            }
            set 
            {
                isReadOnly = value;
                OnPropertyChanged("IsReadOnly");
            }
        }

        protected void OnErrorsChanged(DataErrorsChangedEventArgs e)
        {
            EventHandler<DataErrorsChangedEventArgs> handler = ErrorsChanged;
            if (handler != null) handler(this, e);
        }
    }
}
