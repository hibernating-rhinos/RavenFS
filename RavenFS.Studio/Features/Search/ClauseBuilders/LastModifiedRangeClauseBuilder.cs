using System;
using System.Diagnostics;
using System.Globalization;
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
    public class LastModifiedRangeClauseBuilder : SearchClauseBuilder
    {
        private static readonly string DateIndexFormat = "yyyy-MM-dd_HH-mm-ss";

        public LastModifiedRangeClauseBuilder()
        {
            Description = "Last Modified...";
        }

        public override Model GetInputModel()
        {
            return new LastModifiedRangeClauseModel();
        }

        public override string GetSearchClauseFromModel(Model model)
        {
            var rangeModel = model as LastModifiedRangeClauseModel;
            Debug.Assert(rangeModel != null);

            var lowerLimit = ParseDateAndConvertToSortableFormat(rangeModel.LowerLimit, DateTime.MinValue);
            var upperLimit = ParseDateAndConvertToSortableFormat(rangeModel.UpperLimit, DateTime.MaxValue);

            return string.Format("__modified:[{0} TO {1}]", lowerLimit, upperLimit);
        }

        private static string ParseDateAndConvertToSortableFormat(string value, DateTime @default)
        {
            DateTime result;
            return DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None,
                                     out result)
                       ? result.ToString(DateIndexFormat, CultureInfo.CurrentCulture)
                       : @default.ToString(DateIndexFormat, CultureInfo.CurrentCulture);
        }
    }
}
