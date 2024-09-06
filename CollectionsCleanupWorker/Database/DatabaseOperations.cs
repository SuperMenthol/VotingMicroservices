using MongoDB.Driver;

namespace CollectionsCleanupWorker.Database
{
    public class DatabaseOperations : IDatabaseOperations
    {
        private readonly MongoClient dbClient;
        private readonly IMongoDatabase database;

        public DatabaseOperations(IConfiguration mongoDbConfiguration)
        {
            dbClient = new MongoClient(mongoDbConfiguration.GetSection("MongoDb")["Server"]);
            database = dbClient.GetDatabase(mongoDbConfiguration.GetSection("MongoDb")["Database"]);
        }

        public async Task<bool> CollectionExists(string routingKey)
            => (await database.ListCollectionNames().ToListAsync()).Contains(routingKey);

        public async Task<IMongoCollection<T>?> GetCollection<T>(string collectionName)
        {
            var a = await database.ListCollectionNames().ToListAsync();
            IMongoCollection<T>? result = null;
            if (await CollectionExists(collectionName))
            {
                result = database.GetCollection<T>(collectionName);
            }
            return result;
        }

        public async Task DeleteCollection(string collectionName)
        {
            await database.DropCollectionAsync(collectionName);
        }
    }
}
