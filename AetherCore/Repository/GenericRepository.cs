using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Linq.Expressions;
using AetherCore.DataAccess;
using AetherCore.DTO;
using AetherCore.Entities;
using AetherCore.Exceptions;
using AetherCore.Utility.Caches;
using AetherCore.Utility.Lincense;

namespace AetherCore.Repository
{
    public class GenericRepository<TEntity, TDataAccess>
        where TEntity : IDBEntity where TDataAccess : IDataAccess<TEntity>
    {
        private readonly string _cachePrefix = typeof(TEntity).Name;

        protected readonly TDataAccess _dataAccess;
        protected readonly IMemoryCache _cache;
        protected readonly int _cacheDurationMinutes;

        private bool _bCacheEnable;

        // 建構子，注入資料存取層、快取系統與設定檔
        public GenericRepository(TDataAccess dataAccess, IMemoryCache memoryCache, IOptions<CacheSettings> cacheSettings)
        {
            _dataAccess             = dataAccess;
            _cache                  = memoryCache;
            _cacheDurationMinutes   = cacheSettings.Value.CacheDurationMinutes;
            _bCacheEnable           = cacheSettings.Value.CacheEnable;
        }

        // 建立實體並寫入快取
        public virtual async Task<TEntity> InsertAsync(TEntity entity)
        {
            try
            {
                TEntity? result = await _dataAccess.InsertAsync(entity);

                if (result == null)
                {
                    throw new InvalidEntityException(typeof(TEntity).Name);
                }

                _cache.Set($"{_cachePrefix}:{result.Id}", result, TimeSpan.FromMinutes(_cacheDurationMinutes));
                _cache.Remove($"{_cachePrefix}:all");
                _cache.Remove($"{_cachePrefix}:findLast");

                return result;
            }
            catch (CustomException)
            {
                throw; // 不包自家 RepositoryException
            }
            catch (Exception ex)
            {
                throw new DatabaseOperationException("Create", typeof(TEntity).Name, ex);
            }
        }

        // 取得所有實體（支援快取）
        public virtual async Task<List<TEntity>> GetAllAsync(bool bCache = true)
        {
            await XPlanLicenseRuntime.EnsureValidOrThrowAsync();

            var cacheKey = $"{_cachePrefix}:all";

            try
            {
                if (_bCacheEnable && bCache && _cache.TryGetValue(cacheKey, out List<TEntity>? cachedList))
                {
                    if (cachedList == null)
                    {
                        throw new CacheMissException(cacheKey);
                    }

                    return cachedList;
                }

                var list = await _dataAccess.QueryAllAsync();

                if (list == null)
                {
                    throw new EntityNotFoundException(typeof(TEntity).Name, "All");
                }

                _cache.Set(cacheKey, list, TimeSpan.FromMinutes(_cacheDurationMinutes));

                return list;
            }
            catch (CustomException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new DatabaseOperationException("GetAll", typeof(TEntity).Name, ex);
            }
        }

        // 依照主鍵查詢（支援快取）
        public virtual async Task<TEntity> GetAsync(string key, bool bCache = true)
        {            
            var cacheKey = $"{_cachePrefix}:{key}";

            try
            {
                if (_bCacheEnable && bCache && _cache.TryGetValue(cacheKey, out TEntity? cachedEntity))
                {
                    if (cachedEntity == null)
                    {
                        throw new CacheMissException(cacheKey);
                    }

                    return cachedEntity;
                }

                var entity = await _dataAccess.QueryAsync(key);

                if (entity == null)
                {
                    throw new EntityNotFoundException(typeof(TEntity).Name, key);
                }

                _cache.Set(cacheKey, entity, TimeSpan.FromMinutes(_cacheDurationMinutes));

                return entity;
            }
            catch (CustomException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new DatabaseOperationException("Get", typeof(TEntity).Name, ex);
            }
        }

        // 根據多個 key 查詢多筆（快取友善）
        public virtual async Task<List<TEntity>> GetAsync(List<string> keys, bool bCache = true)
        {
            if (keys == null || keys.Count == 0)
            {
                throw new InvalidRepositoryArgumentException(nameof(keys), "Keys list cannot be null or empty");
            }

            var resultList = new List<TEntity>();
            var keysToFetchFromDb = new List<string>();

            try
            {
                if (_bCacheEnable && bCache)
                {
                    foreach (var key in keys)
                    {
                        var cacheKey = $"{_cachePrefix}:{key}";
                        if (_cache.TryGetValue(cacheKey, out TEntity? cachedEntity))
                        {
                            if (cachedEntity == null)
                            {
                                throw new CacheMissException(cacheKey);
                            }

                            resultList.Add(cachedEntity);
                        }
                        else
                        {
                            keysToFetchFromDb.Add(key);
                        }
                    }
                }
                else
                {
                    keysToFetchFromDb = keys;
                }

                if (keysToFetchFromDb.Count > 0)
                {
                    var dbEntities = await _dataAccess.QueryAsync(keysToFetchFromDb);

                    if (dbEntities == null || dbEntities.Count == 0)
                    {
                        throw new EntityNotFoundException(typeof(TEntity).Name, string.Join(", ", keysToFetchFromDb));
                    }

                    foreach (var entity in dbEntities)
                    {
                        if (entity != null)
                        {
                            var cacheKey = $"{_cachePrefix}:{entity.Id}";
                            _cache.Set(cacheKey, entity, TimeSpan.FromMinutes(_cacheDurationMinutes));
                            resultList.Add(entity);
                        }
                    }
                }

                if (resultList.Count != keys.Count)
                {
                    var missingKeys = keys.Except(resultList.Select(e => e.Id)).ToList();

                    if (missingKeys.Any())
                    {
                        throw new EntityNotFoundException(typeof(TEntity).Name, string.Join(", ", missingKeys));
                    }
                }

                return resultList;
            }
            catch (CustomException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new DatabaseOperationException("Get(List)", typeof(TEntity).Name, ex);
            }
        }

        // 使用 LINQ 條件式查詢
        public virtual async Task<List<TEntity>?> GetAsync(Expression<Func<TEntity, bool>> predicate)
        {
            return await _dataAccess.QueryAsync(predicate);
        }

        // 用時間篩選查詢建立時間區間內的資料
        public virtual async Task<List<TEntity>> GetByTimeAsync(DateTime? startTime = null, DateTime? endTime = null)
        {
            if (startTime.HasValue && endTime.HasValue && startTime > endTime)
            {
                throw new InvalidRepositoryArgumentException(
                    "startTime, endTime",
                    "Start time cannot be later than end time"
                );
            }

            try
            {
                Expression<Func<TEntity, bool>> predicate = e =>
                            (!startTime.HasValue || e.CreatedAt >= startTime.Value) &&
                            (!endTime.HasValue || e.CreatedAt <= endTime.Value);

                var result = await _dataAccess.QueryAsync(predicate);

                if (result == null || result.Count == 0)
                {
                    string rangeDescription = (startTime.HasValue && endTime.HasValue)
                        ? $"{startTime.Value:yyyy-MM-dd HH:mm:ss} ~ {endTime.Value:yyyy-MM-dd HH:mm:ss}"
                        : "specified time range";

                    throw new EntityNotFoundException(typeof(TEntity).Name, rangeDescription);
                }

                return result;
            }
            catch (CustomException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new DatabaseOperationException("GetByTime", typeof(TEntity).Name, ex);
            }
        }

        // 更新實體，同步更新快取
        public virtual async Task<bool> UpdateAsync(string key, TEntity entity)
        {
            try
            {
                bool bResult = await _dataAccess.UpdateAsync(key, entity);

                if (bResult)
                {
                    _cache.Set($"{_cachePrefix}:{key}", entity, TimeSpan.FromMinutes(_cacheDurationMinutes));
                    _cache.Remove($"{_cachePrefix}:all");
                    _cache.Remove($"{_cachePrefix}:exists:{key}");
                    _cache.Remove($"{_cachePrefix}:findLast");
                }

                return bResult;
            }
            catch (CustomException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new DatabaseOperationException("Update", typeof(TEntity).Name, ex);
            }
        }

        // 刪除實體並清除快取
        public virtual async Task DeleteAsync(string key)
        {
            try
            {
                bool bResult = await _dataAccess.DeleteAsync(key);

                if (!bResult)
                {
                    throw new EntityNotFoundException(typeof(TEntity).Name, key);
                }

                _cache.Remove($"{_cachePrefix}:{key}");
                _cache.Remove($"{_cachePrefix}:all");
                _cache.Remove($"{_cachePrefix}:exists:{key}");
                _cache.Remove($"{_cachePrefix}:findLast");
            }
            catch (CustomException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new DatabaseOperationException("Delete", typeof(TEntity).Name, ex);
            }
        }

        // 判斷某 key 是否存在（支援快取）
        public virtual async Task<bool> ExistsAsync(string key, bool bCache = true)
        {
            var cacheKey = $"{_cachePrefix}:exists:{key}";

            if (_bCacheEnable && bCache && _cache.TryGetValue(cacheKey, out bool cachedExists))
            {
                return cachedExists;
            }

            try
            {
                bool exists = await _dataAccess.ExistsAsync(key);

                _cache.Set(cacheKey, exists, TimeSpan.FromMinutes(_cacheDurationMinutes));

                return exists;
            }
            catch (CustomException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new DatabaseOperationException("Exist", typeof(TEntity).Name, ex);
            }
        }

        // 判斷多個 key 是否皆存在（支援快取）
        public virtual async Task<bool> ExistsAsync(List<string> keys, bool bCache = true)
        {
            if (keys == null || keys.Count == 0)
            {
                return false;
            }

            var missingKeys = new List<string>();

            if (_bCacheEnable && bCache)
            {
                foreach (var key in keys)
                {
                    var cacheKey = $"{_cachePrefix}:exists:{key}";
                    if (!_cache.TryGetValue(cacheKey, out bool cachedExists) || !cachedExists)
                    {
                        missingKeys.Add(key);
                    }
                }

                if (missingKeys.Count == 0)
                {
                    return true;
                }
            }
            else
            {
                missingKeys = keys;
            }

            try
            {
                bool allExist = await _dataAccess.ExistsAsync(missingKeys);

                foreach (var key in missingKeys)
                {
                    _cache.Set($"{_cachePrefix}:exists:{key}", allExist, TimeSpan.FromMinutes(_cacheDurationMinutes));
                }

                return allExist;
            }
            catch (CustomException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new DatabaseOperationException("Exists", typeof(TEntity).Name, ex);
            }
        }

        // 取得最新一筆資料（可快取）
        public virtual async Task<TEntity> FindLastAsync(bool bCache = true)
        {
            var cacheKey = $"{_cachePrefix}:findLast";

            try
            {
                if (_bCacheEnable && bCache && _cache.TryGetValue(cacheKey, out TEntity? cachedEntity))
                {
                    if (cachedEntity == null)
                    {
                        throw new CacheMissException(cacheKey);
                    }

                    return cachedEntity;
                }

                var lastEntity = await _dataAccess.FindLastAsync();

                if (lastEntity != null)
                {
                    _cache.Set(cacheKey, lastEntity, TimeSpan.FromMinutes(_cacheDurationMinutes));
                }
                else
                {
                    throw new InvalidOperationException("");
                }

                return lastEntity;
            }
            catch (CustomException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new DatabaseOperationException("FindLastAsync", typeof(TEntity).Name, ex);
            }
        }

        /*==============================
         * 快取清理流程
         * ============================*/
        protected void InvalidateCache(string key)
        {
            _cache.Remove($"{_cachePrefix}:{key}");
            _cache.Remove($"{_cachePrefix}:all");
            _cache.Remove($"{_cachePrefix}:findLast");
            _cache.Remove($"{_cachePrefix}:exists:{key}");
        }
    }
}
