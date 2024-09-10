using CollectionsCleanupWorker.Database;
using MongoDB.Driver;
using Shared.Models;

namespace CollectionsCleanupWorker
{
    public class CollectionsCleanupWorker : BackgroundService
    {
        private readonly ILogger<CollectionsCleanupWorker> logger;
        private readonly IDatabaseOperations databaseOperations;
        private readonly string ProceduresCollectionName;
        private readonly string ResultsCollectionName;

        public CollectionsCleanupWorker(ILogger<CollectionsCleanupWorker> logger, IDatabaseOperations databaseOperations, IConfiguration configuration)
        {
            this.logger=logger;
            this.databaseOperations=databaseOperations;
            var mongoDbConfiguration = configuration.GetRequiredSection("MongoDb");
            ProceduresCollectionName=mongoDbConfiguration[nameof(ProceduresCollectionName)];
            ResultsCollectionName=mongoDbConfiguration[nameof(ResultsCollectionName)];
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            //TaskAwaiter.Wait(30); // uncomment when using TestApp

            while (!stoppingToken.IsCancellationRequested)
            {
                var inactiveProceduresFilter = Builders<ProcedureModel>.Filter.Eq("IsActive", false);

                var collection = await databaseOperations.GetCollection<ProcedureModel>(ProceduresCollectionName);
                var inactiveProcedures = await collection.Find(inactiveProceduresFilter).ToListAsync();
                foreach (var procedure in inactiveProcedures.Where(x => x.LatestResults.OptionResults.Any())) // means that the final scores were processed by worker yet
                {
                    var resultsByRoutingKey = Builders<VotingResultsModel>.Filter.Eq("RoutingKey", procedure.RoutingKey);
                    var resultsCollection = await databaseOperations.GetCollection<VotingResultsModel>(ResultsCollectionName);

                    var anyVotesAfterLatestResults = Builders<VotingResultsModel>.Filter.And(
                        resultsByRoutingKey,
                        Builders<VotingResultsModel>.Filter.Gte("CreatedAt", procedure.LatestResults.CreatedAt));

                    if (!(await resultsCollection.Find(anyVotesAfterLatestResults).ToListAsync()).Any())
                    {
                        await resultsCollection!.DeleteManyAsync(resultsByRoutingKey);
                        logger.LogInformation($"Previous voting results for {procedure.RoutingKey} deleted.");
                        Console.WriteLine($"Previous voting results for {procedure.RoutingKey} deleted.");

                        await databaseOperations.DeleteCollection(procedure.RoutingKey);
                        logger.LogInformation($"Procedure with {procedure.RoutingKey} was deleted from MongoDB.");
                        Console.WriteLine($"Procedure with {procedure.RoutingKey} was deleted from MongoDB.");
                    }
                }

                await Task.Delay(60000 * 3, stoppingToken);
            }
        }
    }
}