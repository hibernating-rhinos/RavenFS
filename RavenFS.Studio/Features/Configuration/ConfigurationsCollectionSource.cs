using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
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

namespace RavenFS.Studio.Features.Configuration
{
    public class ConfigurationsCollectionSource : VirtualCollectionSource<ConfigurationModel>
    {
        private string prefix;

        public ConfigurationsCollectionSource()
        {
            prefix = "";
        }

        protected override Task<IList<ConfigurationModel>> GetPageAsyncOverride(int start, int pageSize, IList<SortDescription> sortDescriptions)
        {
            return ApplicationModel.Current.Client.Config
                            .SearchAsync(prefix, start, pageSize).ContinueOnSuccess(t =>
                                {
                                    SetCount(t.TotalCount);
                                    return (IList<ConfigurationModel>)t.ConfigNames.Select(n => new ConfigurationModel(n)).ToList();
                                });
        }

        public string Prefix
        {
            get { return prefix; }
            set
            {
                if (prefix == value)
                {
                    return;
                }
                prefix = value;
                Refresh(RefreshMode.ClearStaleData);
            }
        }
    }
}
