using System;

namespace AetherCore.Entities
{
    // 所有資料實體需實作的基本資料結構
    public interface IDBEntity
    {
        string Id { get; set; }             // 唯一識別碼
        DateTime CreatedAt { get; set; }    // 建立時間
        DateTime UpdatedAt { get; set; }    // 最後更新時間
    }
}
