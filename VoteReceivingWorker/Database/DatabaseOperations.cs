using MongoDB.Driver;
using Shared;

namespace VoteReceivingWorker.Database
{
    public class DatabaseOperations : IDatabaseOperations
    {
        private readonly MongoClient dbClient;
        private readonly IMongoDatabase database;

        public DatabaseOperations(IConfigurationSection mongoDbConfiguration)
        {
            dbClient = new MongoClient(mongoDbConfiguration["Server"]);
            database = dbClient.GetDatabase(mongoDbConfiguration["Database"]);
        }

        public async Task<bool> HasPersonVotedInThisProcedure(string routingKey, string personId)
            => (await database.GetCollection<VoteModel>(routingKey).FindAsync(x => x.PersonId == personId)).Any();

        public async Task CreateProcedureCollection(string routingKey)
        {
            await database.CreateCollectionAsync(routingKey);
        }
    }
}
