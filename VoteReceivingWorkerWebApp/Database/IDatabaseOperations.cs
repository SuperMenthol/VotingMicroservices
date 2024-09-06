using Shared.Models;

namespace VoteReceivingWorker.Database
{
    public interface IDatabaseOperations
    {
        Task<bool> CollectionExists(string routingKey);
        Task<bool> HasPersonVotedInThisProcedure(string routingKey, string personId);
        Task InsertDocument(string routingKey, VoteModel vote);
    }
}