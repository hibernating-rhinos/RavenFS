using System;
using RavenFS.Studio.Infrastructure;
using RavenFS.Studio.Extensions;

namespace RavenFS.Studio.Features.Search.ClauseBuilders
{
    public class FileNameEndsWithClauseBuilder : SearchClauseBuilder
    {
        public FileNameEndsWithClauseBuilder()
        {
            Description = "File Name Ends With...";
        }

        public override Model GetInputModel()
        {
            return new SingleInputSearchClauseModel() {InputName = "Ends With:", Input = ""};
        }

        public override string GetSearchClauseFromModel(Model model)
        {
            var singleInputModel = model as SingleInputSearchClauseModel;
            if (singleInputModel == null)
            {
                throw new InvalidOperationException();
            }

            return "__rfileName:" + singleInputModel.Input.Reverse() + "*";
        }
    }
}
