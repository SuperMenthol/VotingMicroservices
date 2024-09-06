using MongoDB.Driver;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Models;
using StatsWorker.Database;
using System.Text;
using System.Text.Json;

namespace StatsWorker
{
    public class StatsRequestWorker : BaseStatsWorker, IHostedService
    {
        private readonly ILogger<StatsRequestWorker> logger;
        private readonly IDatabaseOperations databaseOperations;
        private readonly string ProcedureCollectionName;
        private readonly string ResultsCollectionName;

        private readonly string ScoringRequestQueueName;
        private readonly string QueueName;
        private readonly string Exchange;

        public StatsRequestWorker(ILogger<StatsRequestWorker> logger, IDatabaseOperations databaseOperations, IConfiguration configuration) : base(databaseOperations, logger)
        {
            this.logger=logger;
            this.databaseOperations=databaseOperations;
            var rabbitMqConfiguration = configuration.GetRequiredSection("RabbitMq");
            ScoringRequestQueueName = rabbitMqConfiguration[nameof(ScoringRequestQueueName)];
            QueueName = rabbitMqConfiguration[nameof(QueueName)];
            Exchange = rabbitMqConfiguration[nameof(Exchange)];

            var mongoDbConfiguration = configuration.GetRequiredSection("MongoDb");
            ProcedureCollectionName=mongoDbConfiguration[nameof(ProcedureCollectionName)];
            ResultsCollectionName=mongoDbConfiguration[nameof(ResultsCollectionName)];
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
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

                await Task.Delay(5000, cancellationToken);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
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
                        await resultsCollection.InsertOneAsync(updateResult);

                        PublishResults(channel, new List<VotingResultsModel> { updateResult }, QueueName, Exchange);
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
