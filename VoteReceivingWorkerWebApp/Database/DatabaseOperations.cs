using MongoDB.Driver;
using Shared.Models;

namespace VoteReceivingWorker.Database
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

        public async Task<bool> HasPersonVotedInThisProcedure(string routingKey, string personId)
            => (await database.GetCollection<VoteModel>(routingKey).FindAsync(x => x.PersonId == personId)).Any();

        public async Task InsertDocument(string routingKey, VoteModel vote)
        {
            await database.GetCollection<VoteModel>(routingKey).InsertOneAsync(vote);
        }
    }
}
