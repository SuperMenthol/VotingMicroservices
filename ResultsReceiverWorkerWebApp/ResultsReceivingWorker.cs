using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Helpers;
using System.Text;

namespace ResultsReceiverWorkerWebApp
{
    public class ResultsReceivingWorker : BackgroundService
    {
        private readonly string Exchange;
        private readonly string QueueName;
        private readonly string RoutingKey;

        public ResultsReceivingWorker(IConfiguration configuration)
        {
            var rabbitMqConfiguration = configuration.GetRequiredSection("RabbitMq");
            QueueName = rabbitMqConfiguration[nameof(QueueName)];
            Exchange = rabbitMqConfiguration[nameof(Exchange)];
            RoutingKey = rabbitMqConfiguration[nameof(RoutingKey)];
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            TaskAwaiter.Wait(60);

            while (!stoppingToken.IsCancellationRequested)
            {
                var connectionFactory = new ConnectionFactory();

                using var connection = connectionFactory.CreateConnection();
                using var channel = connection.CreateModel();

                if (connection.IsOpen)
                {
                    ConsumeResults(channel);
                }

                await Task.Delay(60000 * 5, stoppingToken);
            }
        }

        private void ConsumeResults(IModel channel)
        {
            channel.QueueBind(QueueName, Exchange, RoutingKey);
            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += ReceivedAction();
            channel.BasicConsume(QueueName, autoAck: true, consumer: consumer);
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
