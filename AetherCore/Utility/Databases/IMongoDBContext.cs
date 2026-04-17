using MongoDB.Driver;

namespace AetherCore.Utility.Databases
{
    // MongoDB 資料庫上下文介面，提供取得指定集合的方法
    public interface IMongoDbContext
    {
        // 取得指定名稱的 MongoDB 集合
        IMongoCollection<T> GetCollection<T>(string name);
    }
}
