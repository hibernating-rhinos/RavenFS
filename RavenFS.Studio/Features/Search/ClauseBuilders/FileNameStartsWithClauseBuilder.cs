using System;
using RavenFS.Studio.Infrastructure;

namespace RavenFS.Studio.Features.Search.ClauseBuilders
{
    public class FileNameStartsWithClauseBuilder : SearchClauseBuilder
    {
        public FileNameStartsWithClauseBuilder()
        {
            Description = "File Name Starts With...";
        }

        public override Model GetInputModel()
        {
            return new SingleInputSearchClauseModel() {InputName = "Starts With:"};
        }

        public override string GetSearchClauseFromModel(Model model)
        {
            var singleInputModel = model as SingleInputSearchClauseModel;
            if (singleInputModel == null)
            {
                throw new InvalidOperationException();
            }

            return "__fileName:" + singleInputModel.Input + "*";
        }
    }
}
