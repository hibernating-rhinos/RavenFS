using System;
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

namespace RavenFS.Studio.Commands
{
    public class VirtualItemCommand<T> : Command where T : class
    {
        private readonly Observable<VirtualItem<T>> observableItem;
        private VirtualItem<T> currentItem;
 
        public VirtualItemCommand(Observable<VirtualItem<T>> observableItem)
        {
            this.observableItem = observableItem;
            observableItem.PropertyChanged += HandleObservableItemChanged;
        }

        private void HandleObservableItemChanged(object sender, PropertyChangedEventArgs e)
        {
            if (currentItem != null)
            {
                currentItem.PropertyChanged -= HandleVirtualItemChanged;
            }

            currentItem = observableItem.Value;

            if (currentItem != null)
            {
                currentItem.PropertyChanged += HandleVirtualItemChanged;
            }

            RaiseCanExecuteChanged();
        }

        private void HandleVirtualItemChanged(object sender, PropertyChangedEventArgs e)
        {
            RaiseCanExecuteChanged();
        }

        public sealed override bool CanExecute(object parameter)
        {
            return observableItem.Value != null 
                && observableItem.Value.IsRealized 
                && !observableItem.Value.IsStale
                && CanExecuteOverride(currentItem.Item);
        }

        protected virtual bool CanExecuteOverride(T item)
        {
            return true;
        }

        public sealed override void Execute(object parameter)
        {
            if (CanExecute(null))
            {
                ExecuteOverride(currentItem.Item);
            }
        }

        protected virtual void ExecuteOverride(T item)
        {
            
        }

    }
}
