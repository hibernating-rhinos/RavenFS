using System.Diagnostics;
using RavenFS.Studio.Infrastructure;
using RavenFS.Studio.Extensions;

namespace RavenFS.Studio.Features.Search.ClauseBuilders
{
    public class HasMetadataClauseBuilder : SearchClauseBuilder
    {
        public HasMetadataClauseBuilder()
        {
            Description = "Has Metadata...";
        }

        public override ViewModel GetInputModel()
        {
            return new HasMetadataClauseModel();
        }

        public override string GetSearchClauseFromModel(ViewModel model)
        {
            var metadataModel = model as HasMetadataClauseModel;
            Debug.Assert(metadataModel != null);

	        if (metadataModel.SelectedField.IsNullOrEmpty() || metadataModel.SearchPattern.IsNullOrEmpty())
		        return "";

	        return metadataModel.SelectedField + ":" + metadataModel.SearchPattern;
        }
    }
}
