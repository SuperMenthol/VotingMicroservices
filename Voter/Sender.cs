using RabbitMQ.Client;
using Shared.Models;
using System.Text.Json;
using Voter.Abstractions;

namespace VotingMicroservices
{
    internal class Sender : ISender
    {
        private readonly ConnectionFactory connectionFactory;
        private readonly IConfigurationSection rabbitMqSection;

        public Sender()
        {
            var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
            rabbitMqSection = configuration.GetSection("RabbitMq");

            connectionFactory = new ConnectionFactory
            {
                HostName = rabbitMqSection["HostName"]
            };
        }

        public bool Send(string routingKey, VoteModel model)
        {
            try
            {
                using var connection = connectionFactory.CreateConnection();
                using var channel = connection.CreateModel();

                channel.QueueDeclare(rabbitMqSection["QueueName"], true, false, false);

                var body = JsonSerializer.SerializeToUtf8Bytes(model);

                channel.BasicPublish(
                    exchange: rabbitMqSection["Exchange"],
                    routingKey: routingKey,
                    basicProperties: null,
                    body: body);

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}