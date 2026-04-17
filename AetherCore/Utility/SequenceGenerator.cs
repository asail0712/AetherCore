using MongoDB.Bson;
using MongoDB.Driver;
using AetherCore.Utility.Databases;

namespace AetherCore.Utility
{
    public interface ISequenceGenerator
    {
        /// <summary>
        /// 取得指定名稱的下一個流水號字串，會自動遞增
        /// </summary>
        /// <param name="name">流水號名稱 (counter id)</param>
        /// <param name="startValue">起始值，預設 1</param>
        /// <param name="digits">流水號長度，不足補零，預設 3</param>
        /// <returns>格式化後的流水號字串</returns>
        Task<string> GetNextSequenceAsync(string name, long startValue = 1, int digits = 3);
    }

    public class SequenceGenerator : ISequenceGenerator
    {
        private readonly IMongoCollection<BsonDocument> _counterCollection;

        public SequenceGenerator(IMongoDbContext dbContext)
        {
            // 專門存放所有流水號的 MongoDB 集合
            _counterCollection = dbContext.GetCollection<BsonDocument>("counters");
        }

        public async Task<string> GetNextSequenceAsync(string name, long startValue = 1, int digits = 3)
        {
            var filter  = Builders<BsonDocument>.Filter.Eq("_id", name);
            var update  = Builders<BsonDocument>.Update.Inc("seq", 1);
            var options = new FindOneAndUpdateOptions<BsonDocument>
            {
                ReturnDocument  = ReturnDocument.After,     // 回傳更新後的文件
                IsUpsert        = true                      // 如果找不到文件就新增
            };

            var result = await _counterCollection.FindOneAndUpdateAsync(filter, update, options);

            // 新增後若不包含 seq 欄位，代表第一次初始化，設定起始值
            if (!result.Contains("seq"))
            {
                var initUpdate  = Builders<BsonDocument>.Update.Set("seq", startValue);
                result          = await _counterCollection.FindOneAndUpdateAsync(filter, initUpdate, options);
            }

            long sequenceNumber = result["seq"].AsInt32;

            // 回傳格式化後字串，不足位數補 0
            return sequenceNumber.ToString($"D{digits}");
        }
    }
}
