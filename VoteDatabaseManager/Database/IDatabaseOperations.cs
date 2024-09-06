using MongoDB.Driver;

namespace VoteDatabaseManager.Database
{
    public interface IDatabaseOperations
    {
        Task<bool> CollectionExists(string routingKey);
        Task InsertDocument<T>(string routingKey, T document);
        Task<IMongoCollection<T>?> GetCollection<T>(string collectionName);
        Task CreateCollection(string collectionName);
        Task DeleteCollection(string collectionName);
    }
}