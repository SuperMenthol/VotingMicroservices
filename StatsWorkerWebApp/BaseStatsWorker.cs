using MongoDB.Driver;
using RabbitMQ.Client;
using Shared.Models;
using StatsWorker.Database;
using System.Text.Json;

namespace StatsWorker
{
    public abstract class BaseStatsWorker
    {
        private readonly IDatabaseOperations databaseOperations;
        private readonly ILogger logger;

        public BaseStatsWorker(IDatabaseOperations databaseOperations, ILogger logger)
        {
            this.databaseOperations = databaseOperations;
            this.logger = logger;
        }

        protected async Task<VotingResultsModel?> UpdateResultsFor(ProcedureModel procedure)
        {
            VotingResultsModel result = null;

            var collection = await databaseOperations.GetCollection<VoteModel>(procedure.RoutingKey);
            if (collection == null)
            {
                logger.LogError($"Voting collection with key {procedure.RoutingKey} was not found.");
                return result;
            }

            var votesCreatedAfterLastResults = Builders<VoteModel>.Filter
                .Gte("CreatedAt", procedure.LatestResults?.CreatedAt ?? new DateTime());

            var votesAfterOrDuringLastResult = await collection.Find(votesCreatedAfterLastResults).ToListAsync();
            if (votesAfterOrDuringLastResult.Any())
            {
                var updatedResultsModel = new VotingResultsModel(DateTime.Now,
                    procedure.RoutingKey,
                    RecalculateVotes(procedure.LatestResults!.OptionResults, votesAfterOrDuringLastResult));

                result = updatedResultsModel;
            }

            return result;
        }

        protected IEnumerable<OptionResultModel> RecalculateVotes(IEnumerable<OptionResultModel> currentOptionResults, IEnumerable<VoteModel> votes)
        {
            var newTotal = currentOptionResults.Sum(x => x.Count) + votes.Count();

            var newVoteResults = new List<OptionResultModel>();
            var grouping = votes.GroupBy(x => x.OptionId).ToList();

            foreach (var option in currentOptionResults)
            {
                var sum = option.Count + votes.Where(x => x.OptionId == option.OptionId).Count();
                var updatedOptionResult = new OptionResultModel(option.OptionId, option.Name, sum, (decimal)sum/(decimal)newTotal);
                newVoteResults.Add(updatedOptionResult);
            }

            return newVoteResults;
        }

        protected void PublishResults(IModel channel, List<VotingResultsModel> newResults, string queueName, string exchange)
        {
            channel.QueueDeclare(queueName, true, false, false, new Dictionary<string, object> { { "x-message-ttl", 43200000 } });
            foreach (var result in newResults)
            {
                channel.BasicPublish(
                    exchange: exchange,
                    routingKey: result.ProcedureKey,
                    body: JsonSerializer.SerializeToUtf8Bytes(result),
                    basicProperties: null);
            }
        }
    }
}
