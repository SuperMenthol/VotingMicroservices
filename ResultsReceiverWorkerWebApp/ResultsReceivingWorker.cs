using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace ResultsReceiverWorker
{
    public class ResultsReceivingWorker : BackgroundService
    {
        private readonly string Exchange;
        private readonly string QueueName;
        private readonly string RoutingKey;

        private readonly ConnectionFactory connectionFactory;
        private readonly IModel channel;
        private readonly EventingBasicConsumer consumer;

        public ResultsReceivingWorker(IConfiguration configuration)
        {
            var rabbitMqConfiguration = configuration.GetRequiredSection("RabbitMq");
            QueueName = rabbitMqConfiguration[nameof(QueueName)];
            Exchange = rabbitMqConfiguration[nameof(Exchange)];
            RoutingKey = rabbitMqConfiguration[nameof(RoutingKey)];

            connectionFactory = new ConnectionFactory();

            var connection = connectionFactory.CreateConnection();
            channel = connection.CreateModel();

            consumer = new EventingBasicConsumer(channel);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            //TaskAwaiter.Wait(15); // uncomment when using TestApp
            ConsumeResults();
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(60000 * 5, stoppingToken);
            }
        }

        private void ConsumeResults()
        {
            channel.QueueDeclare(
                queue: QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: new Dictionary<string, object> { { "x-max-age", "1D" }, { "x-queue-type", "stream" } });
            channel.QueueBind(QueueName, Exchange, RoutingKey);
            consumer.Received += ReceivedAction();
            channel.BasicQos(0, 5, false);
            channel.BasicConsume(QueueName, autoAck: false, consumer: consumer);
        }

        private EventHandler<BasicDeliverEventArgs> ReceivedAction()
        {
            return (ch, ea) =>
            {
                var messageBody = Encoding.UTF8.GetString(ea.Body.ToArray());
                Console.WriteLine(messageBody);
            };
        }
    }
}
