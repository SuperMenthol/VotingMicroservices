using MongoDB.Driver;
using RabbitMQ.Client;
using Shared.Helpers;
using Shared.Models;
using StatsWorker.Database;

namespace StatsWorker
{
    public class StatsWorker : BaseStatsWorker, IHostedService
    {
        private readonly ILogger<StatsWorker> logger;
        private readonly IDatabaseOperations databaseOperations;

        private readonly string Exchange;
        private readonly string QueueName;

        private readonly string ProcedureCollectionName;
        private readonly string ResultsCollectionName;

        public StatsWorker(ILogger<StatsWorker> logger, IDatabaseOperations databaseOperations, IConfiguration configuration) : base(databaseOperations, logger)
        {
            this.logger = logger;
            this.databaseOperations = databaseOperations;

            var rabbitMqConfiguration = configuration.GetRequiredSection("RabbitMq");
            this.Exchange = rabbitMqConfiguration[nameof(Exchange)];
            this.QueueName = rabbitMqConfiguration[nameof(QueueName)];

            var mongoConfiguration = configuration.GetRequiredSection("MongoDb");
            this.ProcedureCollectionName = mongoConfiguration[nameof(ProcedureCollectionName)];
            this.ResultsCollectionName = mongoConfiguration[nameof(ResultsCollectionName)];
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            TaskAwaiter.Wait(60);
            var connectionFactory = new ConnectionFactory();

            while (!cancellationToken.IsCancellationRequested)
            {
                logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                Console.WriteLine($"Worker running at: {DateTimeOffset.Now}");

                var connection = connectionFactory.CreateConnection();
                var channel = connection.CreateModel();

                try
                {
                    var statistics = await UpdateStatistics();
                    logger.LogInformation($"Updated {statistics.Count()} results in the database.");
                    Console.WriteLine($"Updated {statistics.Count()} results in the database.");
                    PublishResults(channel, statistics, QueueName, Exchange);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex.Message);
                }
                logger.LogInformation("Worker finished at: {time}", DateTimeOffset.Now);
                Console.WriteLine($"Worker finished at: {DateTimeOffset.Now}");
                await Task.Delay(5000, cancellationToken);
                //await Task.Delay(60000 * 15, cancellationToken);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        private async Task<List<VotingResultsModel>> UpdateStatistics()
        {
            var result = new List<VotingResultsModel>();

            var proceduresCollection = await databaseOperations.GetCollection<ProcedureModel>(ProcedureCollectionName);
            if (proceduresCollection == null)
            {
                throw new InvalidDataException("Procedures collection not found. Exiting worker");
            }

            var resultsCollection = await databaseOperations.GetCollection<VotingResultsModel>(ResultsCollectionName);
            if (resultsCollection == null)
            {
                throw new InvalidDataException("Results collection not found. Exiting worker");
            }

            var procedures = (await proceduresCollection.Find(_ => true).ToListAsync())
                .ToList();

            foreach (var procedure in procedures)
            {
                var collection = await databaseOperations.GetCollection<VoteModel>(procedure.RoutingKey);
                if (collection == null)
                {
                    logger.LogError($"Voting collection with key {procedure.RoutingKey} was not found.");
                    continue;
                }

                var updateResult = await UpdateResultsFor(procedure);

                if (updateResult != null)
                {
                    result.Add(updateResult);
                    var routingKeyMatching = Builders<ProcedureModel>.Filter.Eq("RoutingKey", procedure.RoutingKey);
                    var updateLatestResults = Builders<ProcedureModel>.Update.Set(x => x.LatestResults, updateResult);

                    await proceduresCollection.UpdateOneAsync(routingKeyMatching, updateLatestResults);
                }
            }

            await resultsCollection.InsertManyAsync(result);

            return result;
        }
    }
}
