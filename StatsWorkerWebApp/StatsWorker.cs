using MongoDB.Driver;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Models;
using StatsWorker.Database;
using System.Text;
using System.Text.Json;

namespace StatsWorker
{
    // TODO: Split back into two when fix for https://github.com/dotnet/runtime/issues/38751 is implemented
    public class StatsWorker : BaseStatsWorker, IHostedService
    {
        private readonly ILogger<StatsWorker> logger;
        private readonly IDatabaseOperations databaseOperations;

        private readonly string ScoringRequestExchange;
        private readonly string ScoringRequestQueueName;
        private readonly string Exchange;
        private readonly string QueueName;

        private readonly string ProcedureCollectionName;
        private readonly string ResultsCollectionName;

        public StatsWorker(ILogger<StatsWorker> logger, IDatabaseOperations databaseOperations, IConfiguration configuration) : base(databaseOperations, logger)
        {
            this.logger = logger;
            this.databaseOperations = databaseOperations;

            var rabbitMqConfiguration = configuration.GetRequiredSection("RabbitMq");
            ScoringRequestExchange = rabbitMqConfiguration[nameof(ScoringRequestExchange)];
            ScoringRequestQueueName = rabbitMqConfiguration[nameof(ScoringRequestQueueName)];
            this.Exchange = rabbitMqConfiguration[nameof(Exchange)];
            this.QueueName = rabbitMqConfiguration[nameof(QueueName)];

            var mongoConfiguration = configuration.GetRequiredSection("MongoDb");
            this.ProcedureCollectionName = mongoConfiguration[nameof(ProcedureCollectionName)];
            this.ResultsCollectionName = mongoConfiguration[nameof(ResultsCollectionName)];
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            //TaskAwaiter.Wait(10); // uncomment when using TestApp

            logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            Console.WriteLine($"Worker running at: {DateTimeOffset.Now}");

            var channel = Connect();

            StartListening(channel);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await UpdateFromDatabase(channel);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex.Message);
                }
                logger.LogInformation("Worker finished at: {time}", DateTimeOffset.Now);
                Console.WriteLine($"Worker finished at: {DateTimeOffset.Now}");

                await Task.Delay(60000 * 15, cancellationToken);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private IModel Connect()
        {
            var connectionFactory = new ConnectionFactory();

            var connection = connectionFactory.CreateConnection();
            return connection.CreateModel();
        }

        private void StartListening(IModel channel)
        {
            try
            {
                channel.QueueDeclare(
                    ScoringRequestQueueName,
                    true,
                    false,
                    false,
                    arguments: new Dictionary<string, object> { { "x-message-ttl", 43200000 }, { "x-single-active-consumer", true } });

                channel.QueueBind(
                    queue: ScoringRequestQueueName,
                    exchange: ScoringRequestExchange,
                    routingKey: ScoringRequestQueueName,
                    arguments: new Dictionary<string, object> { { "x-message-ttl", 43200000 }, { "x-single-active-consumer", true } });
                var consumer = new EventingBasicConsumer(channel);
                consumer.Received += ReceivedAction(channel);

                channel.BasicConsume(
                    queue: ScoringRequestQueueName,
                    autoAck: false,
                    consumer: consumer);
            }
            catch (Exception ex)
            {
                logger.LogError(ex.Message);
            }
        }

        private async Task UpdateFromDatabase(IModel channel)
        {
            var statistics = await UpdateStatistics();
            logger.LogInformation($"Updated {statistics.Count()} results in the database.");
            Console.WriteLine($"Updated {statistics.Count()} results in the database.");
            PublishResults(channel, statistics, QueueName, Exchange);
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

        private EventHandler<BasicDeliverEventArgs> ReceivedAction(IModel channel)
        {
            return async (ch, ea) =>
            {
                try
                {
                    Console.WriteLine($"Received request for results preparation for: {Encoding.UTF8.GetString(ea.Body.ToArray())}");

                    var body = JsonSerializer.Deserialize<RemovalRequestModel>(ea.Body.ToArray());

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

                    var byRoutingKey = Builders<ProcedureModel>.Filter.Eq("RoutingKey", body!.routingKey);
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
                        await resultsCollection.InsertOneAsync(updateResult);

                        PublishResults(channel, new List<VotingResultsModel> { updateResult }, QueueName, ScoringRequestExchange);
                    }

                    channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            };
        }
    }
}
