using System;
using System.Collections.Generic;
using System.Linq;

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
