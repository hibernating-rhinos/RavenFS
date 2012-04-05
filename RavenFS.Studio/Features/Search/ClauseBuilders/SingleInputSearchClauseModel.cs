using RavenFS.Studio.Infrastructure;

namespace RavenFS.Studio.Features.Search.ClauseBuilders
{
    public class SingleInputSearchClauseModel : Model
    {
        string input;

        public string InputName { get; set; }

        public string Input
        {
            get { return input; }
            set
            {
                input = value;
                OnPropertyChanged("Input");
            }
        }

        public string Example { get; set; }
    }
}
