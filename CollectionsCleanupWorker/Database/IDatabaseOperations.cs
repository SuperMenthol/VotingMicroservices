using MongoDB.Driver;

namespace CollectionsCleanupWorker.Database
{
    public interface IDatabaseOperations
    {
        Task<bool> CollectionExists(string routingKey);
        Task<IMongoCollection<T>?> GetCollection<T>(string collectionName);
        Task DeleteCollection(string collectionName);
    }
}