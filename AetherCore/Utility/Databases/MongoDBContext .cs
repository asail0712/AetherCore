using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace AetherCore.Utility.Databases
{
    // MongoDB 資料庫上下文實作，負責建立並提供資料庫集合的存取
    public class MongoDBContext : IMongoDbContext
    {
        private readonly IMongoDatabase _database;

        // 建構子，透過 DI 注入設定與 MongoDB 用戶端，並取得指定資料庫
        public MongoDBContext(IOptions<MongoDBSettings> settings, IMongoClient client)
        {
            _database = client.GetDatabase(settings.Value.DatabaseName);
        }

        // 取得指定名稱的 MongoDB 集合
        public IMongoCollection<T> GetCollection<T>(string name)
        {
            return _database.GetCollection<T>(name);
        }
    }
}
