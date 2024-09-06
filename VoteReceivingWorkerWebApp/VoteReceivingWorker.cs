using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Helpers;
using Shared.Models;
using System.Text;
using System.Text.Json;
using VoteReceivingWorkerWebApp.Database;

namespace VoteReceivingWorkerWebApp
{
    public class VoteReceivingWorker : BackgroundService
    {
        private readonly ILogger<VoteReceivingWorker> _logger;
        private readonly IDatabaseOperations databaseOperations;

        private readonly string QueueName;

        public VoteReceivingWorker(ILogger<VoteReceivingWorker> logger, IDatabaseOperations dbOperations, IConfigurationSection rabbitMqConfiguration)
        {
            _logger = logger;
            this.QueueName = rabbitMqConfiguration[nameof(QueueName)];
            this.databaseOperations = dbOperations;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            TaskAwaiter.Wait(5);

            var connectionFactory = new ConnectionFactory();
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

                StartConsuming(connectionFactory);
                await Task.Delay(60000, stoppingToken);
            }
        }

        private void StartConsuming(ConnectionFactory connectionFactory)
        {
            var connection = connectionFactory.CreateConnection();
            var channel = connection.CreateModel();

            channel.QueueDeclare(QueueName, true, false, false);
            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += ReceivedAction(channel);

            channel.BasicConsume(
                queue: QueueName,
                autoAck: false,
                consumer: consumer);
        }

        private EventHandler<BasicDeliverEventArgs> ReceivedAction(IModel channel)
        {
            return async (ch, ea) =>
            {
                try
                {
                    Console.WriteLine($"Received message: {Encoding.UTF8.GetString(ea.Body.ToArray())}");
                    _logger.LogInformation($"Received message: {Encoding.UTF8.GetString(ea.Body.ToArray())}");

                    if (!await databaseOperations.CollectionExists(ea.RoutingKey))
                    {
                        _logger.LogInformation($"Message routing key {ea.RoutingKey} does not point to existing voting procedure.");
                    }

                    var voteModel = JsonSerializer.Deserialize<VoteModel>(ea.Body.ToArray());
                    if (await databaseOperations.HasPersonVotedInThisProcedure(ea.RoutingKey, voteModel!.PersonId))
                    {
                        _logger.LogWarning($"Person with id of {voteModel.PersonId} tried to vote more than once in procedure with identifier {ea.RoutingKey}");
                    }
                    else
                    {
                        await databaseOperations.InsertDocument(ea.RoutingKey, voteModel);
                    }

                    channel.BasicAck(ea.DeliveryTag, false);
                }
                catch
                {

                }
            };
        }
    }
}