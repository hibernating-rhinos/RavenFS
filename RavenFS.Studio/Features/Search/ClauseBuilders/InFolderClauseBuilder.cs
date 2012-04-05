using System;
using System.Diagnostics;
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
using RavenFS.Studio.Extensions;

namespace RavenFS.Studio.Features.Search.ClauseBuilders
{
    public class InFolderClauseBuilder : SearchClauseBuilder
    {
        public InFolderClauseBuilder()
        {
            Description = "In Folder...";
        }

        public override Model GetInputModel()
        {
            return new SingleInputSearchClauseModel() {InputName = "Folder Path", Example = "E.g. /folder/subfolder"};
        }

        public override string GetSearchClauseFromModel(Model model)
        {
            var inputModel = model as SingleInputSearchClauseModel;
            Debug.Assert(inputModel != null);

            if (inputModel.Input.IsNullOrEmpty())
            {
                return "";
            }
            else
            {
                return "__directory:" + inputModel.Input;
            }
        }

    }
}
