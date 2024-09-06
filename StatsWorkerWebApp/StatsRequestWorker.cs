using MongoDB.Driver;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Models;
using StatsWorkerWebApp.Database;
using System.Text;
using System.Text.Json;

namespace StatsWorkerWebApp
{
    public class StatsRequestWorker : BackgroundService
    {
        private readonly ILogger<StatsRequestWorker> logger;
        private readonly IDatabaseOperations databaseOperations;
        private readonly string ScoringRequestQueueName;
        private readonly string ProcedureCollectionName;
        private readonly string ResultsCollectionName;

        public StatsRequestWorker(ILogger<StatsRequestWorker> logger, IDatabaseOperations databaseOperations, IConfiguration configuration)
        {
            this.logger=logger;
            this.databaseOperations=databaseOperations;
            var rabbitMqConfiguration = configuration.GetRequiredSection("RabbitMq");
            ScoringRequestQueueName=rabbitMqConfiguration[nameof(ScoringRequestQueueName)];

            var mongoDbConfiguration = configuration.GetRequiredSection("MongoDb");
            ProcedureCollectionName=mongoDbConfiguration[nameof(ProcedureCollectionName)];
            ResultsCollectionName=mongoDbConfiguration[nameof(ResultsCollectionName)];
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var connectionFactory = new ConnectionFactory();

                var connection = connectionFactory.CreateConnection();
                var channel = connection.CreateModel();

                channel.QueueDeclare(ScoringRequestQueueName, true, false, false);
                var consumer = new EventingBasicConsumer(channel);
                consumer.Received += ReceivedAction(channel);

                channel.BasicConsume(
                    queue: ScoringRequestQueueName,
                    autoAck: false,
                    consumer: consumer);

                await Task.Delay(5000, stoppingToken);
            }
        }

        private EventHandler<BasicDeliverEventArgs> ReceivedAction(IModel channel)
        {
            return async (ch, ea) =>
            {
                try
                {
                    Console.WriteLine($"Received request for results preparation for: {Encoding.UTF8.GetString(ea.Body.ToArray())}");

                    var body = JsonSerializer.Deserialize<RemovalRequestModel>(ea.Body.ToArray());

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

                    var byRoutingKey = Builders<ProcedureModel>.Filter.Eq("RoutingKey", body.routingKey);
                    var procedure = (await proceduresCollection.Find(byRoutingKey).SingleAsync());

                    var collection = await databaseOperations.GetCollection<VoteModel>(procedure.RoutingKey);
                    if (collection == null)
                    {
                        logger.LogError($"Voting collection with key {procedure.RoutingKey} was not found.");
                        return;
                    }

                    var updateResult = await UpdateResultsFor(procedure);

                    if (updateResult != null)
                    {
                        var routingKeyMatching = Builders<ProcedureModel>.Filter.Eq("RoutingKey", procedure.RoutingKey);
                        var updateLatestResults = Builders<ProcedureModel>.Update.Set(x => x.LatestResults, updateResult);

                        await proceduresCollection.UpdateOneAsync(routingKeyMatching, updateLatestResults);
                    }

                    channel.BasicAck(ea.DeliveryTag, false);
                }
                catch
                {

                }
            };
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
