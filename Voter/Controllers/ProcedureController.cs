using Microsoft.AspNetCore.Mvc;
using RestEase;
using Shared.Models;
using Voter.Abstractions;

namespace Voter.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ProcedureController : ControllerBase
    {
        private readonly IManagerAPI restClient;
        private readonly ILogger<ProcedureController> _logger;

        public ProcedureController(ILogger<ProcedureController> logger, IConfiguration configuration)
        {
            _logger = logger;
            restClient = RestClient.For<IManagerAPI>(configuration["VoteDatabaseManagerUrl"]);
        }

        [HttpGet("active-procedures")]
        public async Task<Dictionary<string, string>> GetActiveProcedures()
            => await restClient.GetActiveProcedures();

        [HttpGet("options")]
        public async Task<List<OptionModel>> GetProcedureOptions([FromQuery] string routingKey)
            => await restClient.GetProcedureOptions(routingKey);

        [HttpGet("info")]
        public async Task<ProcedureModel> GetProcedureInformation([FromQuery] string routingKey)
            => await restClient.GetProcedureInfo(routingKey);
    }
}