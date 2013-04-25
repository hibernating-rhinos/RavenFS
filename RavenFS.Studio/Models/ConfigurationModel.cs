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
