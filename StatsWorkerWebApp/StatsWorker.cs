using MongoDB.Driver;
using RabbitMQ.Client;
using Shared.Helpers;
using Shared.Models;
using StatsWorkerWebApp.Database;
using System.Text.Json;

namespace StatsWorkerWebApp
{
    public class StatsWorker : BackgroundService
    {
        private readonly ILogger<StatsWorker> logger;
        private readonly IDatabaseOperations databaseOperations;

        private readonly string Exchange;
        private readonly string QueueName;

        private readonly string ProcedureCollectionName;
        private readonly string ResultsCollectionName;

        public StatsWorker(ILogger<StatsWorker> logger, IDatabaseOperations databaseOperations, IConfiguration configuration)
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

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            TaskAwaiter.Wait(60);
            var connectionFactory = new ConnectionFactory();

            while (!stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                Console.WriteLine($"Worker running at: {DateTimeOffset.Now}");

                try
                {
                    var statistics = await UpdateStatistics();
                    logger.LogInformation($"Updated {statistics.Count()} results in the database.");
                    Console.WriteLine($"Updated {statistics.Count()} results in the database.");
                    PublishResults(connectionFactory, statistics);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex.Message);
                }
                logger.LogInformation("Worker finished at: {time}", DateTimeOffset.Now);
                Console.WriteLine($"Worker finished at: {DateTimeOffset.Now}");
                await Task.Delay(60000 * 15, stoppingToken);
            }
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

            var mostVotesFirst = Builders<ProcedureModel>.Sort
                .Descending(x => x.LatestResults.OptionResults.Sum(x => x.Count));
            var procedures = (await proceduresCollection.Find(null).Sort(mostVotesFirst).ToListAsync())
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
                    var routingKeyMatching = Builders<ProcedureModel>.Filter.Eq("RoutingKey", procedure.RoutingKey);
                    var updateLatestResults = Builders<ProcedureModel>.Update.Set(x => x.LatestResults, updateResult);

                    await proceduresCollection.UpdateOneAsync(routingKeyMatching, updateLatestResults);
                }
            }

            await resultsCollection.InsertManyAsync(result);

            return result;
        }

        private async Task<VotingResultsModel?> UpdateResultsFor(ProcedureModel procedure)
        {
            VotingResultsModel result = null;

            var collection = await databaseOperations.GetCollection<VoteModel>(procedure.RoutingKey);
            if (collection == null)
            {
                logger.LogError($"Voting collection with key {procedure.RoutingKey} was not found.");
            }

            var votesCreatedAfterLastResults = Builders<VoteModel>.Filter
                .AnyGte("CreatedAt", procedure.LatestResults?.CreatedAt ?? new DateTime());

            var votesAfterOrDuringLastResult = await collection.Find(votesCreatedAfterLastResults).ToListAsync();
            if (votesAfterOrDuringLastResult.Any())
            {
                var updatedResultsModel = new VotingResultsModel(DateTime.Now,
                    procedure.RoutingKey,
                    RecalculateVotes(procedure.LatestResults?.OptionResults ?? new List<OptionResultModel>(), votesAfterOrDuringLastResult));

                result = updatedResultsModel;
            }

            return result;
        }

        private void PublishResults(ConnectionFactory connectionFactory, List<VotingResultsModel> newResults)
        {
            using var connection = connectionFactory.CreateConnection();
            using var channel = connection.CreateModel();

            channel.QueueDeclare(QueueName, true, false, false);
            foreach (var result in newResults)
            {
                channel.BasicPublish(
                    exchange: Exchange,
                    routingKey: result.ProcedureKey,
                    body: JsonSerializer.SerializeToUtf8Bytes(result),
                    basicProperties: null);
            }
        }

        private IEnumerable<OptionResultModel> RecalculateVotes(IEnumerable<OptionResultModel> currentOptionResults, IEnumerable<VoteModel> votes)
        {
            var newTotal = currentOptionResults.Sum(x => x.Count) + votes.Count();

            var newVoteResults = new List<OptionResultModel>();
            var grouping = votes.GroupBy(x => x.OptionId).ToList();

            foreach (var group in grouping)
            {
                var currentOptionResult = currentOptionResults.First(x => x.OptionId == group.Key);
                var sum = currentOptionResult.Count + group.Count();
                newVoteResults.Add(new(group.Key, currentOptionResult.Name, sum, (decimal)sum/(decimal)newTotal));
            }

            return newVoteResults;
        }
    }
}
