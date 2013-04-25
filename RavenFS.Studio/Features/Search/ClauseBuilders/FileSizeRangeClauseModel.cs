using RavenFS.Studio.Infrastructure;

namespace RavenFS.Studio.Features.Search.ClauseBuilders
{
    public class FileSizeRangeClauseModel : ViewModel
    {
        string lowerLimit;
        string upperLimit;

        public string LowerLimit
        {
            get { return lowerLimit; }
            set
            {
                lowerLimit = value;
                OnPropertyChanged(() => LowerLimit);
            }
        }

        public string UpperLimit
        {
            get { return upperLimit; }
            set
            {
                upperLimit = value;
                OnPropertyChanged(() => UpperLimit);
            }
        }
    }
}
