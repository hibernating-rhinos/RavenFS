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
using RavenFS.Studio.Behaviors;
using RavenFS.Studio.Features.Search.ClauseBuilders;
using RavenFS.Studio.Infrastructure;
using RavenFS.Studio.Extensions;

namespace RavenFS.Studio.Features.Search
{
    public class SearchClauseBuilderModel : ViewModel
    {
        public static string DisplayPopupMessage = "DisplayPopup";

        private readonly SearchClauseBuilder builder;
        private readonly Action<string> addSearchClause;
        private ICommand displayCommand;
        private object inputView;
        bool isViewOpen;
        private ViewModel inputModel;
        private ICommand completeCommand;

        public SearchClauseBuilderModel(SearchClauseBuilder builder, Action<string> addSearchClause)
        {
            this.builder = builder;
            this.addSearchClause = addSearchClause;
        }

        public string Description { get { return builder.Description; } }

        public ICommand Display { get { return displayCommand ?? (displayCommand = new ActionCommand(HandleDisplay)); } }
        public ICommand Complete { get { return completeCommand ?? (completeCommand = new ActionCommand(HandleComplete)); } }

        private void HandleDisplay()
        {
            inputModel = builder.GetInputModel();
            InputView = GetInputViewForModel(inputModel);
            OnUIMessage(new UIMessageEventArgs(DisplayPopupMessage));
        }

        private FrameworkElement GetInputViewForModel(ViewModel inputModel)
        {
            var inputModelType = inputModel.GetType();
            var viewTypeName = inputModelType.Namespace + "." + inputModelType.Name + "View";

            var viewType = Type.GetType(viewTypeName);
            if (viewType == null)
            {
                throw new ArgumentException(string.Format("Could not find view for inputModel. Expected to find UserControl called '{0}'", viewTypeName));
            }

            var view = Activator.CreateInstance(viewType) as FrameworkElement;
            if (view == null)
            {
                throw new ArgumentException(string.Format("Could not find view for inputModel. Expected to find UserControl called '{0}'", viewTypeName));
            }

            view.DataContext = inputModel;

            return view;
        }


        public object InputView
        {
            get { return inputView; }
            private set
            {
                inputView = value;
                OnPropertyChanged(() => InputView);
            }
        }

        private void HandleComplete()
        {
            var clause = builder.GetSearchClauseFromModel(inputModel);
            if (!clause.IsNullOrEmpty())
            {
                addSearchClause(clause);
            }
         }
    }
}
