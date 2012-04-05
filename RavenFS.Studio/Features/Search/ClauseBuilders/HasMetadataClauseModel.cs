using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
using RavenFS.Studio.Models;
using RavenFS.Studio.Extensions;

namespace RavenFS.Studio.Features.Search.ClauseBuilders
{
    public class HasMetadataClauseModel : Model
    {
        private static readonly HashSet<string> ExcludedTerms = new HashSet<string>() { "Content-Length", "Last-Modified" };
 
        string selectedField;
        string searchPattern;

        public HasMetadataClauseModel()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (ApplicationModel.Current.State.CachedSearchTerms != null)
            {
                AvailableFields = new ObservableCollection<string>(ApplicationModel.Current.State.CachedSearchTerms);
            }
            else
            {
                AvailableFields = new ObservableCollection<string>();
            }

            ApplicationModel.Current.Client
                .GetSearchFieldsAsync(0, 1024)
                .ContinueOnUIThread(
                    t =>
                        {
                            if (!t.IsFaulted)
                            {
                                AvailableFields.UpdateFromOrdered(
                                    t.Result.Where(term => !term.StartsWith("__") && !ExcludedTerms.Contains(term))
                                        .OrderBy(x => x),
                                    x => x);

                                ApplicationModel.Current.State.CachedSearchTerms =
                                    AvailableFields.ToArray();
                            }
                        });
        }

        public ObservableCollection<string> AvailableFields { get; set; }

        public string SelectedField
        {
            get { return selectedField; }
            set
            {
                selectedField = value;
                OnPropertyChanged("SelectedField");
            }
        }

        public string SearchPattern
        {
            get { return searchPattern; }
            set
            {
                searchPattern = value;
                OnPropertyChanged("SearchPattern");
            }
        }
    }
}
