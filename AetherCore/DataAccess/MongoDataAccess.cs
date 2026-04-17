using AetherCore.DTO;
using AetherCore.Entities;
using AetherCore.Utility.Databases;
using AetherCore.Utility.Lincense;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Linq.Expressions;

namespace AetherCore.DataAccess
{
    public abstract class MongoDataAccess<TEntity>// : IDataAccess<TEntity> 
        where TEntity : IDBEntity
    {
        private readonly IMongoCollection<TEntity> _collection;
        private bool _bIndexCreated             = false;
        private static string _searchFieldName  = "Id";
        private List<string> _noUpdateList;

        public MongoDataAccess(IMongoDbContext dbContext)
        {
            this._collection    = dbContext.GetCollection<TEntity>(typeof(TEntity).Name);
            this._noUpdateList  = new List<string>();
        }

        // 加入不參與更新的欄位名稱
        protected void AddNoUpdateKey(string noUpdateKey)
        {
            _noUpdateList.Add(noUpdateKey);
            _noUpdateList.Distinct(); // 去除重複值（此行實際上沒生效，要用回傳值才有用）
        }

        // 確保只建立一次索引
        protected void EnsureIndexCreated(string searchFieldName)
        {
            if (!_bIndexCreated)
            {
                _bIndexCreated      = true;
                _searchFieldName    = searchFieldName;

                var indexKeys       = Builders<TEntity>.IndexKeys.Ascending(searchFieldName);
                var indexOptions    = new CreateIndexOptions { Unique = true };
                var indexModel      = new CreateIndexModel<TEntity>(indexKeys, indexOptions);

                _collection.Indexes.CreateOne(indexModel);
            }
        }

        // 插入一筆資料
        public virtual async Task<TEntity?> InsertAsync(TEntity entity)
        {
            await XPlanLicenseRuntime.EnsureValidOrThrowAsync();

            await _collection.InsertOneAsync(entity);

            return entity;
        }

        // 查詢全部資料
        public virtual async Task<List<TEntity>?> QueryAllAsync()
        {
            return await _collection.Find(_ => true).ToListAsync();
        }

        // 查詢單筆資料
        public virtual async Task<TEntity?> QueryAsync(string key)
        {
            var baseFilter = Builders<TEntity>.Filter.Eq(_searchFieldName, key);

            return await _collection.Find(baseFilter).FirstOrDefaultAsync();
        }

        // 查詢多筆資料
        public virtual async Task<List<TEntity>?> QueryAsync(List<string> keys)
        {
            if (keys == null || keys.Count == 0)
            {
                return null;
            }

            var baseFilter = Builders<TEntity>.Filter.In(_searchFieldName, keys);

            return await _collection.Find(baseFilter).ToListAsync();
        }

        // 使用 Expression 條件查詢
        public virtual async Task<List<TEntity>?> QueryAsync(Expression<Func<TEntity, bool>> predicate)
        {
            var entities = await _collection.Find(predicate)
                                       .ToListAsync();

            return entities;
        }

        // 更新單筆資料，排除特定欄位
        public virtual async Task<bool> UpdateAsync(string key, TEntity entity)
        {
            var filter          = Builders<TEntity>.Filter.Eq(_searchFieldName, key);
            entity.UpdatedAt    = DateTime.UtcNow;
            var bsonDoc         = entity.ToBsonDocument(); // 將 Entity 轉成 BsonDocument

            // 欄位黑名單：_id、CreatedAt、自定義排除欄位
            var excludedFields  = new HashSet<string>(
                            new[] { "_id", "CreatedAt", _searchFieldName }
                            .Concat(_noUpdateList ?? Enumerable.Empty<string>())
                        ).Distinct();

            // 過濾出允許更新的欄位
            var updateFields    = bsonDoc
                        .Where(kv => !excludedFields.Contains(kv.Name))
                        .ToDictionary(kv => kv.Name, kv => kv.Value);

            if (!updateFields.Any())
            {
                // 沒有要更新的欄位
                return false;
            }

            var update = new BsonDocument("$set", new BsonDocument(updateFields)); // 用 $set 更新欄位
            var result = await _collection.UpdateOneAsync(filter, update);

            return result.ModifiedCount > 0;
        }

        // 刪除單筆資料
        public virtual async Task<bool> DeleteAsync(string key)
        {
            var filter = Builders<TEntity>.Filter.Eq(_searchFieldName, key);
            var result = await _collection.DeleteOneAsync(filter);

            return result.DeletedCount > 0;
        }

        // 檢查單筆是否存在
        public virtual async Task<bool> ExistsAsync(string key)
        {
            var filter  = Builders<TEntity>.Filter.Eq(_searchFieldName, key);
            var count   = await _collection.CountDocumentsAsync(filter);
            return count > 0;
        }

        // 檢查多筆是否存在
        public virtual async Task<bool> ExistsAsync(List<string> key)
        {
            if (key == null || key.Count == 0)
            {
                return false;
            }

            var filter  = Builders<TEntity>.Filter.In(_searchFieldName, key);
            var count   = await _collection.CountDocumentsAsync(filter);

            return count > 0;
        }

        // 查詢最後更新的一筆資料（根據 UpdatedAt）
        public virtual async Task<TEntity?> FindLastAsync()
        {
            var sort = Builders<TEntity>.Sort.Descending(nameof(IDBEntity.UpdatedAt));

            return await _collection
                            .Find(_ => true)
                            .Sort(sort)
                            .Limit(1)
                            .FirstOrDefaultAsync();
        }

        // 查詢並針對每筆資料執行更新邏輯
        public virtual async Task<List<TEntity>?> QueryAndUpdateAsync(Expression<Func<TEntity, bool>> predicate, Action<TEntity> updateAction)
        {
            // 查詢所有符合條件的 Entity
            var entities = await _collection.Find(predicate).ToListAsync();

            if (entities == null || entities.Count == 0)
            {
                return new List<TEntity>();
            }

            // 執行更新邏輯並逐一更新資料庫
            foreach (var entity in entities)
            {
                updateAction(entity);

                var filter = Builders<TEntity>.Filter.Eq(e => e.Id, entity.Id); // 假設有 Id 屬性
                await _collection.ReplaceOneAsync(filter, entity);
            }

            return entities;
        }
        public async Task<TEntity> FindOneAndUpdateAsync(FindUpdateDefinition<TEntity> definition)
        {
            var filter  = definition.Filter(Builders<TEntity>.Filter);
            var update  = definition.Update(Builders<TEntity>.Update);
            var options = new FindOneAndUpdateOptions<TEntity>
            {
                ReturnDocument = definition.ReturnAfter ? ReturnDocument.After : ReturnDocument.Before
            };

            return await _collection.FindOneAndUpdateAsync(filter, update, options);
        }
    }
}
