using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace VoteReceivingWorker
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IConfigurationSection rabbitMqConfiguration;
        private readonly IConfigurationSection mongoDbConfiguration;

        public Worker(ILogger<Worker> logger, IConfigurationSection rabbitMqConfiguration, IConfigurationSection mongoDbConfiguration)
        {
            _logger = logger;
            this.rabbitMqConfiguration = rabbitMqConfiguration;
            this.mongoDbConfiguration = mongoDbConfiguration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
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

            channel.QueueDeclare(rabbitMqConfiguration["QueueName"], true, false, false);
            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += ReceivedAction(channel);

            channel.BasicConsume(
                queue: rabbitMqConfiguration["QueueName"],
                autoAck: false,
                consumer: consumer);
        }

        private EventHandler<BasicDeliverEventArgs> ReceivedAction(IModel channel)
        {
            return (ch, ea) =>
            {
                Console.WriteLine($"Received message: {Encoding.UTF8.GetString(ea.Body.ToArray())}");
                channel.BasicAck(ea.DeliveryTag, false);
            };
        }
    }
}