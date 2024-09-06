using Microsoft.AspNetCore.Mvc;
using RestEase;
using Shared;
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
        private readonly ISender sender;

        public ProcedureController(ILogger<ProcedureController> logger, ISender sender, IConfiguration configuration)
        {
            _logger = logger;
            this.sender = sender;
            restClient = RestClient.For<IManagerAPI>(configuration["VoteDatabaseManagerUrl"]);
        }

        [HttpGet("active-procedures")]
        public async Task<Dictionary<string, string>> GetActiveProcedures()
        {
            return await restClient.GetActiveProcedures();
        }

        [HttpGet("options")]
        public async Task<List<OptionModel>> GetProcedureOptions([FromQuery] string routingKey)
            => await restClient.GetProcedureOptions(routingKey);

        [HttpGet("info")]
        public async Task<ProcedureModel> GetProcedureInformation([FromQuery] string routingKey)
            => await restClient.GetProcedureInfo(routingKey);
    }
}