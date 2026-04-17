namespace AetherCore.Service
{
    // 定義服務層介面，提供基本 CRUD 及存在檢查功能
    public interface IService<TRequest, TResponse>
    {
        // 建立新資料，傳入請求 DTO，回傳回應 DTO
        Task<TResponse> CreateAsync(TRequest request);

        // 取得所有資料
        Task<List<TResponse>> GetAllAsync();

        // 根據單一鍵值取得資料
        Task<TResponse> GetAsync(string key);

        // 根據多筆鍵值取得資料
        Task<List<TResponse>> GetAsync(List<string> keys);

        // 更新資料，傳入鍵值與請求 DTO
        Task UpdateAsync(string key, TRequest request);

        // 刪除資料，傳入鍵值
        Task DeleteAsync(string key);

        // 檢查多筆 ID 是否存在
        Task<bool> IsExists(List<string> idList);
    }
}
