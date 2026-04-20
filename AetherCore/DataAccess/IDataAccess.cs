using MongoDB.Bson;
using MongoDB.Driver;
using System.Linq.Expressions;
using AetherCore.DTO;
using AetherCore.Entities;
using AetherCore.Utility;

namespace AetherCore.DataAccess
{
    // 定義泛型的資料存取介面，提供常見的 CRUD 與條件操作方法
    public interface IDataAccess<TEntity> where TEntity : IDBEntity
    {
        // 🔹 基本操作 CRUD 🔹

        // 新增單筆資料
        Task<TEntity?> InsertAsync(TEntity entity);

        // 查詢所有資料
        Task<List<TEntity>?> QueryAllAsync();

        // 透過主鍵查詢單筆資料
        Task<TEntity?> QueryAsync(string key);

        // 透過主鍵清單查詢多筆資料
        Task<List<TEntity>?> QueryAsync(List<string> key);

        // 透過主鍵更新資料
        Task<bool> UpdateAsync(string key, TEntity entity);

        // 透過條件式更新多個欄位
        Task<bool> UpdateAsync(
            Expression<Func<TEntity, bool>> predicate,
            params UpdateField<TEntity>[] updates);

        // 透過主鍵刪除資料
        Task<bool> DeleteAsync(string key);

        // 🔹 其他輔助方法 🔹

        // 判斷指定主鍵的資料是否存在
        Task<bool> ExistsAsync(string key);

        // 判斷一組主鍵的資料是否都存在
        Task<bool> ExistsAsync(List<string> key);

        // 查詢目前資料中最後一筆（依照某個排序條件，具體實作端決定）
        Task<TEntity?> FindLastAsync();

        // 🔹 使用 Expression 條件式查詢 🔹

        // 使用 Lambda 條件式查詢符合條件的資料
        Task<List<TEntity>?> QueryAsync(Expression<Func<TEntity, bool>> predicate);
    }
}
