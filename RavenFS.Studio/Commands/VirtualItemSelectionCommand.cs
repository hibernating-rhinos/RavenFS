using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using System.Linq;
using RavenFS.Studio.Extensions;

namespace RavenFS.Studio.Commands
{
    public abstract class VirtualItemSelectionCommand<T> : Command where T:class 
    {
        private readonly ItemSelection<VirtualItem<T>> itemSelection;
        private IList<VirtualItem<T>> currentItems;
 
        public VirtualItemSelectionCommand(ItemSelection<VirtualItem<T>> itemSelection)
        {
            this.itemSelection = itemSelection;
            itemSelection.SelectionChanged += HandleSelectionChanged;
            CacheCurrentItems();
        }

        protected ItemSelection<VirtualItem<T>> ItemSelection
        {
            get { return itemSelection; }
        }

        private void HandleSelectionChanged(object sender, EventArgs e)
        {
            if (currentItems != null)
            {
                foreach (var item in currentItems)
                {
                    item.PropertyChanged -= HandleVirtualItemChanged;
                }
            }

            CacheCurrentItems();

            RaiseCanExecuteChanged();
        }

        private void CacheCurrentItems()
        {
            currentItems = ItemSelection.GetSelectedItems();

            foreach (var item in currentItems)
            {
                item.PropertyChanged += HandleVirtualItemChanged;
            }
        }

        private void HandleVirtualItemChanged(object sender, PropertyChangedEventArgs e)
        {
            RaiseCanExecuteChanged();
        }

        public override sealed bool CanExecute(object parameter)
        {
            return CanExecuteOverride(GetRealizedItems());
        }

        public override sealed void Execute(object parameter)
        {
            ExecuteOverride(GetRealizedItems());
        }

        private List<T> GetRealizedItems()
        {
            return currentItems
                .EmptyIfNull()
                .Where(v => v.IsRealized)
                .Select(v => v.Item)
                .ToList();
        }

        protected virtual bool CanExecuteOverride(IList<T> items)
        {
            return true;
        }

        protected abstract void ExecuteOverride(IList<T> realizedItems);
    }
}
