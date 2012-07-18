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

namespace RavenFS.Studio.Features.Search.ClauseBuilders
{
    public class FileSizeRangeClauseModel : Model
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
