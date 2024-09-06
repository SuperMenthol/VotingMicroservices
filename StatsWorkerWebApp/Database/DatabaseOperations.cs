using MongoDB.Driver;

namespace StatsWorkerWebApp.Database
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
            IMongoCollection<T>? result = null;
            if (await CollectionExists(collectionName))
            {
                result = database.GetCollection<T>(collectionName);
            }
            return result;
        }
    }
}
