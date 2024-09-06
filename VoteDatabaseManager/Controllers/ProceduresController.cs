using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using Shared.Models;
using VoteDatabaseManager.Database;

namespace VoteDatabaseManager.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ProceduresController : ControllerBase
    {
        private readonly ILogger<ProceduresController> _logger;
        private readonly IDatabaseOperations databaseOperations;

        private const string procedureSchemaCollectionName = "procedures";

        public ProceduresController(ILogger<ProceduresController> logger, IDatabaseOperations databaseOperations)
        {
            _logger = logger;
            this.databaseOperations = databaseOperations;
        }

        [HttpGet("options")]
        public async Task<IEnumerable<OptionModel>> GetVotingProcedureOptions([FromQuery] string routingKey)
        {
            var result = new List<OptionModel>();
            var votingProcedureInformation = await GetVotingProcedureInfo(routingKey);
            if (votingProcedureInformation != null)
            {
                result = votingProcedureInformation.Options.ToList();
            }

            return result;
        }

        [HttpGet("info")]
        public async Task<ProcedureModel?> GetVotingProcedureInfo([FromQuery] string routingKey)
        {
            ProcedureModel? result = null;

            var collection = await databaseOperations.GetCollection<ProcedureModel>(procedureSchemaCollectionName);
            if (collection != null)
            {
                var filter = Builders<ProcedureModel>.Filter.Eq("RoutingKey", routingKey);
                result = (await collection.FindAsync(filter)).SingleOrDefault();
            }

            return result;
        }

        [HttpGet("procedures")]
        public async Task<Dictionary<string, string>> GetVotingProceduresKeys()
        {
            var collection = await databaseOperations.GetCollection<ProcedureModel>(procedureSchemaCollectionName);
            return (await collection.Find(null)
                .ToListAsync())
                .ToDictionary(x => x.Name, x => x.RoutingKey);
        }
    }
}