using System.Diagnostics;
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

        public override ViewModel GetInputModel()
        {
            return new SingleInputSearchClauseModel() {InputName = "Folder Path", Example = "E.g. /folder/subfolder"};
        }

        public override string GetSearchClauseFromModel(ViewModel model)
        {
            var inputModel = model as SingleInputSearchClauseModel;
            Debug.Assert(inputModel != null);

	        if (inputModel.Input.IsNullOrEmpty())
		        return "";
	        
			return "__directory:" + inputModel.Input;
        }
    }
}
