using AetherCore.DTO;
using AetherCore.Entities;
using AetherCore.Utility.Lincense;
using AutoMapper;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Entities;
using System.Linq.Expressions;

namespace AetherCore.DataAccess
{
    // 泛型的 Mongo 資料存取基底類別，TEntity 是 Domain Model，TDocument 是 MongoDB 資料實體
    public abstract class MongoEntityDataAccess<TEntity, TDocument>
        where TEntity : class, IDBEntity, new()
        where TDocument : IEntity, IDBEntity, new() // 👈 注意繼承 Entity
    {
        protected readonly IMapper _mapper;
        private bool _bIndexCreated         = false;
        private string _searchFieldName     = "Id";
        private List<string> _noUpdateList;

        // 建構子注入 AutoMapper
        protected MongoEntityDataAccess(IMapper mapper)
        {
            this._mapper        = mapper;
            this._noUpdateList  = new List<string>();
        }

        // 加入不允許被更新的欄位名稱
        protected void AddNoUpdateKey(string noUpdateKey)
        {
            _noUpdateList.Add(noUpdateKey);
            _noUpdateList = _noUpdateList.Distinct().ToList();
        }

        /// <summary>
        /// 建立索引（使用 MongoDB.Entities 的 API）
        /// </summary>
        protected void EnsureIndexCreated(string searchFieldName)
        {
            // 因為Mongodb.Entity只支援使用TDocument上的變數名稱做索引
            // 當需要從外部設定時 就用回MongoDB.Driver去設定索引
            if (!_bIndexCreated)
            {
                _bIndexCreated      = true;
                _searchFieldName    = searchFieldName;

                var indexKeys       = Builders<TDocument>.IndexKeys.Ascending(searchFieldName); // searchFieldName 是 string
                var indexOptions    = new CreateIndexOptions { Unique = true };
                var indexModel      = new CreateIndexModel<TDocument>(indexKeys, indexOptions);

                DB.Collection<TDocument>().Indexes.CreateOne(indexModel);
            }
        }

        // 將 Document 映射為 Entity（支援覆寫）
        // 不可當工具方法使用
        protected virtual Task<TEntity> MapToEntity(TDocument doc, IMapper mapper)
        {
            return Task.FromResult(mapper.Map<TEntity>(doc));
        }

        // 將 Entity 映射為 Document（支援覆寫）
        // 不可當工具方法使用
        protected virtual TDocument MapToDocument(TEntity entity, IMapper mapper)
        {
            return mapper.Map<TDocument>(entity);
        }

        protected virtual Task<List<TEntity>> MapToEntitys(List<TDocument> doc, IMapper mapper)
        {
            return Task.FromResult(mapper.Map<List<TEntity>>(doc));
        }

        // 將 Entity 映射為 Document（支援覆寫）
        // 不可當工具方法使用
        protected virtual List<TDocument> MapToDocuments(List<TEntity> entitys, IMapper mapper)
        {
            return mapper.Map<List<TDocument>>(entitys);
        }

        // 新增一筆資料
        public virtual async Task<TEntity?> InsertAsync(TEntity entity)
        {
            await XPlanLicenseRuntime.EnsureValidOrThrowAsync();

            var doc = MapToDocument(entity, _mapper);
            await doc.SaveAsync();
            return MapToEntity(doc, _mapper).Result;
        }

        public virtual async Task<List<TEntity>?> InsertListAsync(List<TEntity> entitys)
        {
            var docs = MapToDocuments(entitys, _mapper);
            await docs.SaveAsync();
            return MapToEntitys(docs, _mapper).Result;
        }

        // 根據 key 查詢一筆資料
        public virtual async Task<TEntity?> QueryAsync(string key)
        {
            var keyFilter   = Builders<TDocument>.Filter.Eq(_searchFieldName, key);
            var doc         = await DB.Find<TDocument>().Match(keyFilter).ExecuteFirstAsync();

            if (doc == null)
            {
                return null;
            }

            return await MapToEntity(doc, _mapper);
        }

        // 查詢全部資料
        public virtual async Task<List<TEntity>?> QueryAllAsync()
        {
            List<TDocument> docs    = await DB.Find<TDocument>().Match(_ => true).ExecuteAsync();
            var entities            = await Task.WhenAll(docs.Select(doc => MapToEntity(doc, _mapper)));
            return entities.ToList();
        }

        // 查詢多筆指定 keys 的資料
        public virtual async Task<List<TEntity>?> QueryAsync(List<string> keys)
        {
            if (keys == null || keys.Count == 0)
            {
                return null;
            }

            var keyFilter   = Builders<TDocument>.Filter.In(_searchFieldName, keys);
            var docs        = await DB.Find<TDocument>().Match(keyFilter).ExecuteAsync();
            var entities    = await Task.WhenAll(docs.Select(doc => MapToEntity(doc, _mapper)));
            return entities.ToList();
        }

        // 使用 Lambda 表達式查詢
        public virtual async Task<List<TEntity>?> QueryAsync(Expression<Func<TEntity, bool>> predicate)
        {
            // 會炸掉
            var documentPredicate   = _mapper.Map<Expression<Func<TDocument, bool>>>(predicate);
            var docs                = await DB.Find<TDocument>().Match(documentPredicate).ExecuteAsync();
            var entities            = await Task.WhenAll(docs.Select(doc => MapToEntity(doc, _mapper)));
            return entities.ToList();
        }

        // 根據 key 更新實體，排除不更新欄位
        public virtual async Task<bool> UpdateAsync(string key, TEntity entity)
        {
            entity.UpdatedAt    = DateTime.UtcNow;
            var doc             = MapToDocument(entity, _mapper);

            // 排除更新欄位（Id、CreatedAt、自訂欄位）
            var excludedFields  = new HashSet<string>(
                new[] { "_id", "CreatedAt", _searchFieldName }
                .Concat(_noUpdateList ?? Enumerable.Empty<string>())
            ).Distinct();

            doc.Id              = ObjectId.GenerateNewId().ToString(); // 必須指定 Id 才能轉 BsonDocument
            var bsonDoc         = doc.ToBsonDocument();

            var updateDict      = bsonDoc
                .Where(kv => !excludedFields.Contains(kv.Name))
                .ToDictionary(kv => kv.Name, kv => kv.Value);

            if (!updateDict.Any())
            {
                return false;
            }

            var update = DB.Update<TDocument>().Match(d => d.Eq(_searchFieldName, key));

            foreach (var kv in updateDict)
            {
                update = update.Modify(b => b.Set(kv.Key, kv.Value));
            }

            var result = await update.ExecuteAsync();
            return result.ModifiedCount > 0;
        }

        // 根據 key 刪除資料
        public virtual async Task<bool> DeleteAsync(string key)
        {
            var deletedResult = await DB.DeleteAsync<TDocument>(d => d.Eq(_searchFieldName, key));
            return deletedResult.DeletedCount > 0;
        }

        // 判斷某個 key 是否存在
        public virtual async Task<bool> ExistsAsync(string key)
        {
            var count = await DB.CountAsync<TDocument>(d => d.Eq(_searchFieldName, key));
            return count > 0;
        }

        // 判斷多個 key 是否存在
        public virtual async Task<bool> ExistsAsync(List<string> keys)
        {
            if (keys == null || keys.Count == 0)
            {
                return false;
            }

            var count = await DB.CountAsync<TDocument>(d => d.In(_searchFieldName, keys));
            return count > 0;
        }

        // 找出最後更新的那筆資料
        public virtual async Task<TEntity?> FindLastAsync()
        {
            var doc = await DB.Find<TDocument>()
                              .Sort(d => d.Descending(x => x.UpdatedAt))
                              .ExecuteFirstAsync();

            if (doc == null)
            {
                return null;
            }

            return await MapToEntity(doc, _mapper);
        }
    }

    // OneReference Helper：處理 MongoDB.Entities 的 One<T> reference
    public static class OneReferenceHelper
    {
        // 取得 Reference ID
        public static string? GetId<TDocument>(One<TDocument> one)
            where TDocument : IEntity
        {
            return one?.ID;
        }

        // 字串轉 One<T>
        public static One<TDocument> ToOne<TDocument>(this string id)
            where TDocument : IEntity
        {
            return new One<TDocument>(id);
        }

        // Reference 載入 Entity（單筆）
        public static async Task<TDocument> LoadEntityAsync<TDocument>(this One<TDocument> one)
            where TDocument : IEntity
        {
            return await one.ToEntityAsync();
        }

        // Reference 載入 Entity（多筆）
        public static async Task<List<TDocument>> LoadEntitysAsync<TDocument>(this List<One<TDocument>> ones)
            where TDocument : IEntity
        {
            return (await Task.WhenAll(ones.Select(one => one.ToEntityAsync()))).ToList();
        }

        // Entity 轉 Reference
        public static One<TDocument> ToRef<TDocument>(this TDocument entity)
            where TDocument : IEntity
        {
            return entity.ToReference();
        }

        // 多個 Entity 轉 Reference
        public static List<One<TDocument>> ToRef<TDocument>(this List<TDocument> docs)
            where TDocument : IEntity
        {
            return docs.Select(doc => doc.ToReference()).ToList();
        }
    }
}
