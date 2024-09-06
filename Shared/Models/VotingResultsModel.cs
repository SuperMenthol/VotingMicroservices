namespace Shared.Models
{
    public record VotingResultsModel(DateTime CreatedAt, string ProcedureKey, IEnumerable<OptionResultModel> OptionResults);
}