using MongoDB.Driver;
using Shared.Models;
using VoteDatabaseManager.Database;

namespace VoteDatabaseManager
{
    public class RedundantCollectionsCleanupWorker : BackgroundService
    {
        private readonly ILogger<RedundantCollectionsCleanupWorker> logger;
        private readonly IDatabaseOperations databaseOperations;
        private readonly string ProceduresCollectionName;
        private readonly string ResultsCollectionName;

        public RedundantCollectionsCleanupWorker(ILogger<RedundantCollectionsCleanupWorker> logger, IDatabaseOperations databaseOperations, IConfiguration configuration)
        {
            this.logger=logger;
            this.databaseOperations=databaseOperations;
            var mongoDbConfiguration = configuration.GetRequiredSection("MongoDb");
            ProceduresCollectionName=mongoDbConfiguration[nameof(ProceduresCollectionName)];
            ResultsCollectionName=mongoDbConfiguration[nameof(ResultsCollectionName)];
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var inactiveProceduresFilter = Builders<ProcedureModel>.Filter.Eq("ActiveOnly", false);

                var collection = await databaseOperations.GetCollection<ProcedureModel>(ProceduresCollectionName);
                var inactiveProcedures = await collection.Find(inactiveProceduresFilter).ToListAsync();
                foreach (var procedure in inactiveProcedures.Where(x => x.LatestResults.OptionResults.Any())) // means that the final scores were processed by worker yet
                {
                    var resultsByRoutingKey = Builders<VotingResultsModel>.Filter.Eq("RoutingKey", procedure.RoutingKey);
                    var resultsCollection = await databaseOperations.GetCollection<VotingResultsModel>(ResultsCollectionName);

                    var anyVotesAfterLatestResults = Builders<VotingResultsModel>.Filter.And(
                        resultsByRoutingKey,
                        Builders<VotingResultsModel>.Filter.AnyGte("CreatedAt", procedure.LatestResults.CreatedAt));

                    if (!(await resultsCollection.Find(resultsByRoutingKey).ToListAsync()).Any())
                    {
                        await resultsCollection!.DeleteManyAsync(resultsByRoutingKey);
                        logger.LogInformation($"Previous voting results for {procedure.RoutingKey} deleted.");

                        await databaseOperations.DeleteCollection(procedure.RoutingKey);
                        logger.LogInformation($"Procedure with {procedure.RoutingKey} was deleted from MongoDB.");
                    }
                }

                await Task.Delay(3000, stoppingToken);
                //await Task.Delay(60000 * 30, stoppingToken);
            }
        }
    }
}