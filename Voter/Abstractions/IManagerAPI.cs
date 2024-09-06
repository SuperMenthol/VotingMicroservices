using RestEase;
using Shared.Models;

namespace Voter.Abstractions
{
    public interface IManagerAPI
    {
        [Get("procedures/active-procedures")]
        Task<Dictionary<string, string>> GetActiveProcedures();

        [Get("procedures/info")]
        Task<ProcedureModel> GetProcedureInfo([Query] string routingKey);

        [Get("procedures/options")]
        Task<List<OptionModel>> GetProcedureOptions([Query] string routingKey);
    }
}
