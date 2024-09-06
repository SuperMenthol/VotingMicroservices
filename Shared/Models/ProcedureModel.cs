namespace Shared.Models
{
    public record ProcedureModel(string Name, string RoutingKey, string Description, bool IsActive, IEnumerable<OptionModel> Options, VotingResultsModel LatestResults);
}
