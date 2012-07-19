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
    public class ConfigurationModel : ViewModel
    {
        bool isModified;

        public ConfigurationModel(string name)
        {
            Name = name;
        }

        public string Name { get; private set; }

        public bool IsModified
        {
            get { return isModified; }
            set
            {
                isModified = value;
                OnPropertyChanged("IsModified");
            }
        }
    }
}
