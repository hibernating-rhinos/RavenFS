using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Lucene.Net.Analysis;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Version = Lucene.Net.Util.Version;

namespace RavenFS.Search
{
    public class RavenQueryParser : QueryParser
    {
        private readonly HashSet<string> numericFields;

        public RavenQueryParser(Analyzer analyzer, IEnumerable<string> numericFields) : base(Version.LUCENE_29, "", analyzer)
        {
            this.numericFields = new HashSet<string>(numericFields);
        }

        protected override Query NewRangeQuery(string field, string part1, string part2, bool inclusive)
        {
            if (numericFields.Contains(field))
            {
                long lower;
				long upper;

				if (!long.TryParse(part1, out lower))
				{
				    lower = long.MinValue;
				}

				if (!long.TryParse(part2, out upper))
				{
				    upper = long.MaxValue;
				}

                var rangeQuery = NumericRangeQuery.NewLongRange(field, lower, upper, inclusive, inclusive);

                return rangeQuery;
            }

            return base.NewRangeQuery(field, part1, part2, inclusive);
        }
    }
}