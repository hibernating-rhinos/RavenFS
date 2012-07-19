using System;
using System.Globalization;
using System.Net;
using System.Threading;
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
    public class LastModifiedRangeClauseModel : ViewModel
    {
        string lowerLimit;
        string upperLimit;

        public string ExampleDate
        {
            get { return "E.g. " +(new DateTime(2012, 12, 31, 13, 30, 0)).ToString("g", CultureInfo.CurrentCulture); }
        }

        public string LowerLimit
        {
            get { return lowerLimit; }
            set
            {
                lowerLimit = value;
                TryParseAndSet(lowerLimit, d => LowerLimitDate = d);
                OnPropertyChanged(() => LowerLimit);
            }
        }

        public string UpperLimit
        {
            get { return upperLimit; }
            set
            {
                upperLimit = value;
                TryParseAndSet(upperLimit, d => UpperLimitDate = d);
                OnPropertyChanged(() => UpperLimit);
            }
        }

        private void TryParseAndSet(string value, Action<DateTime> setter)
        {
            DateTime result;
            if (DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out result))
            {
                _isSettingDate = true;
                setter(result);
                _isSettingDate = false;
            }
        }

        DateTime? lowerLimitDate;

        public DateTime? LowerLimitDate
        {
            get { return lowerLimitDate; }
            set
            {
                lowerLimitDate = value;
                OnPropertyChanged(() => LowerLimitDate);
                if (!_isSettingDate && lowerLimitDate.HasValue)
                {
                    LowerLimit = lowerLimitDate.Value.ToString("d", CultureInfo.CurrentCulture);
                }
            }
        }

        DateTime? upperLimitDate;
        private bool _isSettingDate;

        public DateTime? UpperLimitDate
        {
            get { return upperLimitDate; }
            set
            {
                upperLimitDate = value;
                OnPropertyChanged(() => UpperLimitDate);
                if (!_isSettingDate && upperLimitDate.HasValue)
                {
                    UpperLimit = upperLimitDate.Value.ToString("d", CultureInfo.CurrentCulture);
                }
            }
        }
    }
}
