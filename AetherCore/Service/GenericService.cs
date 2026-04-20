using AetherCore.Entities;                     // 定義了 IDBEntity 介面
using AetherCore.Repository;                   // 定義了 IRepository 介面
using AutoMapper;                         // 用於物件映射（TRequest ⇔ TEntity ⇔ TResponse）
using AetherCore.Utility.Lincense;
using System.Linq.Expressions;

namespace AetherCore.Service
{
    // 通用的 Service 抽象類別，處理資料的基本 CRUD 操作
    public abstract class GenericService<TEntity, TRequest, TResponse, TRepository>     //: IService<TRequest, TResponse> 
                            where TEntity : IDBEntity                                // 限制 TEntity 必須實作 IDBEntity（需有 Id、CreatedAt、UpdatedAt）
                            where TRepository : IRepository<TEntity>                 // 限制 TRepository 為資料存取介面 IRepository
    {
        protected readonly TRepository _repository;              // 資料存取層
        protected readonly IMapper _mapper;                      // AutoMapper 實體，用於 DTO ⇄ Entity 映射

        public GenericService(TRepository repository, IMapper mapper)
        {
            _repository = repository;
            _mapper     = mapper;
        }

        // 建立新資料
        public virtual async Task<TResponse> CreateAsync(TRequest request)
        {
            var entity  = _mapper.Map<TEntity>(request);            // 將請求映射為 Entity
            entity      = await _repository.InsertAsync(entity);    // 實際呼叫 Repository 建立資料

            return _mapper.Map<TResponse>(entity);                  // 將建立結果映射成回傳格式
        }

        // 取得所有資料
        public virtual async Task<List<TResponse>> GetAllAsync()
        {
            List<TEntity>? entities = await _repository.GetAllAsync();
            return _mapper.Map<List<TResponse>>(entities);
        }

        // 透過單一 ID 取得資料
        public virtual async Task<TResponse> GetAsync(string key)
        {
            var entity = await _repository.GetAsync(key);
            return _mapper.Map<TResponse>(entity);
        }

        // 透過多個 ID 取得資料
        public virtual async Task<List<TResponse>> GetAsync(List<string> keys)
        {
            await XPlanLicenseRuntime.EnsureValidOrThrowAsync();
            var entity = await _repository.GetAsync(keys);
            return _mapper.Map<List<TResponse>>(entity);
        }

        // 更新資料（更新時間一律為現在）
        public virtual async Task UpdateAsync(string key, TRequest request)
        {
            var entity          = _mapper.Map<TEntity>(request);
            await _repository.UpdateAsync(key, entity);
        }

        // 刪除資料
        public virtual async Task DeleteAsync(string key)
        {
            await _repository.DeleteAsync(key);
        }

        // 確認多筆 ID 是否都存在
        public async Task<bool> IsExists(List<string> idList)
        {
            return await _repository.ExistsAsync(idList);
        }

        protected virtual Task<List<TEntity>?> QueryEntitiesAsync(Expression<Func<TEntity, bool>> predicate)
        {
            return _repository.GetAsync(predicate);
        }

        protected virtual async Task<List<TResponse>> QueryResponsesAsync(Expression<Func<TEntity, bool>> predicate)
        {
            var entities = await _repository.GetAsync(predicate) ?? new List<TEntity>();
            return _mapper.Map<List<TResponse>>(entities);
        }
    }
}
