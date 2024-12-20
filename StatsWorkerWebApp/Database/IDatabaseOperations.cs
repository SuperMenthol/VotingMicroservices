﻿using MongoDB.Driver;

namespace StatsWorker.Database
{
    public interface IDatabaseOperations
    {
        Task<bool> CollectionExists(string routingKey);
        Task<IMongoCollection<T>?> GetCollection<T>(string collectionName);
    }
}