using RavenFS.Studio.Infrastructure;

namespace RavenFS.Studio.Features.Search.ClauseBuilders
{
    public abstract class SearchClauseBuilder
    {
        public string Description { get; protected set; }

        public abstract Model GetInputModel();

        public abstract string GetSearchClauseFromModel(Model model);
    }
}
