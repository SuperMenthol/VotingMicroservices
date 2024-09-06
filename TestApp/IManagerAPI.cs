using Microsoft.AspNetCore.Mvc;
using RestEase;
using Shared.Request;

namespace TestApp
{
    public interface IManagerAPI
    {
        [Post("Management/add-procedure")]
        Task<IActionResult> AddProcedure([Body] AddVotingProcedureRequest request);

        [Delete("Management/remove-procedure/{routingKey}")]
        Task<IActionResult> RemoveProcedure([Path] string routingKey);
    }
}
