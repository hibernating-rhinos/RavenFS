using System;
using System.Collections.Generic;
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

namespace RavenFS.Studio.Infrastructure
{
    public class ItemSelection<T> : ItemSelection
    {
        private IList<T> snapshot;
 
        public new IList<T> GetSelectedItems()
        {
            return snapshot ?? (snapshot = base.GetSelectedItems().OfType<T>().ToList());
        }

        protected override void OnSelectionChanged(EventArgs e)
        {
            snapshot = null;
            base.OnSelectionChanged(e);
        }
    }
}
