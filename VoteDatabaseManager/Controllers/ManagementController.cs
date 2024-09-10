using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using RabbitMQ.Client;
using Shared.Models;
using Shared.Request;
using System.Text.Json;
using VoteDatabaseManager.Database;

namespace VoteDatabaseManager.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ManagementController : ControllerBase
    {
        private readonly ILogger<ManagementController> _logger;
        private readonly IDatabaseOperations databaseOperations;

        private readonly string ProceduresCollectionName;
        private readonly string ResultsCollectionName;

        private readonly string Exchange;
        private readonly string QueueName;
        private readonly string ScoringRequestQueueName;

        public ManagementController(ILogger<ManagementController> logger, IDatabaseOperations databaseOperations, IConfiguration configuration)
        {
            _logger = logger;
            this.databaseOperations = databaseOperations;
            var mongoDbConfiguration = configuration.GetRequiredSection("MongoDb");
            this.ProceduresCollectionName = mongoDbConfiguration[nameof(ProceduresCollectionName)];
            this.ResultsCollectionName = mongoDbConfiguration[nameof(ResultsCollectionName)];

            var rabbitMqConfiguration = configuration.GetRequiredSection("RabbitMq");
            this.Exchange = rabbitMqConfiguration[nameof(Exchange)];
            this.QueueName = rabbitMqConfiguration[nameof(QueueName)];
            this.ScoringRequestQueueName = rabbitMqConfiguration[nameof(ScoringRequestQueueName)];
        }

        [HttpPost("add-procedure")]
        public async Task<IActionResult> AddVotingProcedure([FromBody] AddVotingProcedureRequest request)
        {
            try
            {
                _logger.LogInformation($"Adding new procedure. Routing key: {request.Model.Name}, Name: {request.Model.RoutingKey}");

                var collection = await databaseOperations.GetCollection<ProcedureModel>(ProceduresCollectionName);
                var proceduresWithSameNameOrRoutingKey = Builders<ProcedureModel>.Filter.And(
                    Builders<ProcedureModel>.Filter.Or(
                        Builders<ProcedureModel>.Filter.Eq("RoutingKey", request.Model.RoutingKey),
                        Builders<ProcedureModel>.Filter.Eq("Name", request.Model.Name)),
                    Builders<ProcedureModel>.Filter.Eq("IsActive", true));

                if ((await collection.Find(proceduresWithSameNameOrRoutingKey).ToListAsync()).Any())
                {
                    throw new InvalidOperationException("This name and/or routing key is already in use for an active voting procedure.");
                }

                await databaseOperations.InsertDocument(ProceduresCollectionName, request.Model);
                await databaseOperations.CreateCollection(request.Model.RoutingKey);

                _logger.LogInformation($"Procedure with routing key {request.Model.RoutingKey} created successfully.");
                AddRabbitBinding(request.Model.RoutingKey);
                _logger.LogInformation($"RabbitMQ binding {request.Model.RoutingKey} created successfully.");

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                throw;
            }
        }

        [HttpDelete("remove-procedure/{routingKey}")]
        public async Task<IActionResult> RemoveVotingProcedure([FromRoute] string routingKey)
        {
            try
            {
                _logger.LogInformation($"Removing voting procedure with routing key {routingKey}");

                var proceduresCollection = await databaseOperations.GetCollection<ProcedureModel>(ProceduresCollectionName);
                var procedureByRoutingKeyAndActive = Builders<ProcedureModel>.Filter.And(
                    Builders<ProcedureModel>.Filter.Eq("RoutingKey", routingKey),
                    Builders<ProcedureModel>.Filter.Eq("IsActive", true));

                if (!(await (await proceduresCollection.FindAsync(procedureByRoutingKeyAndActive)).ToListAsync()).Any())
                {
                    throw new InvalidOperationException($"There is no active procedure with routing key {routingKey}");
                }
                var updateIsActive = Builders<ProcedureModel>.Update.Set(x => x.IsActive, false);
                await proceduresCollection!.UpdateOneAsync(procedureByRoutingKeyAndActive, updateIsActive);
                _logger.LogInformation($"Procedure {routingKey} is now marked as inactive.");

                RemoveRabbitBinding(routingKey);
                _logger.LogInformation($"Procedure {routingKey} unbound from RabbitMQ.");

                PublishScoringRequestMessage(routingKey);

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                throw;
            }
        }

        private void AddRabbitBinding(string routingKey)
        {
            var connectionFactory = new ConnectionFactory();
            using var connection = connectionFactory.CreateConnection();
            using var channel = connection.CreateModel();

            channel.QueueBind(QueueName, Exchange, routingKey);
        }

        private void RemoveRabbitBinding(string routingKey)
        {
            var connectionFactory = new ConnectionFactory();
            using var connection = connectionFactory.CreateConnection();
            using var channel = connection.CreateModel();

            channel.QueueUnbind(QueueName, Exchange, routingKey);
        }

        private void PublishScoringRequestMessage(string routingKey)
        {
            var connectionFactory = new ConnectionFactory();
            using var connection = connectionFactory.CreateConnection();
            using var channel = connection.CreateModel();

            channel.QueueDeclare(ScoringRequestQueueName, true, false, false, arguments: new Dictionary<string, object>
            {
                { "x-message-ttl", 43200000 },
                { "x-single-active-consumer", true }
            });

            var body = JsonSerializer.SerializeToUtf8Bytes(new RemovalRequestModel(routingKey));
            channel.BasicPublish(
                exchange: Exchange,
                routingKey: ScoringRequestQueueName,
                basicProperties: null,
                body: body);
        }
    }
}