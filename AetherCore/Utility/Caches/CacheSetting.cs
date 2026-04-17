namespace AetherCore.Utility.Caches
{
    // 快取設定類別，用來設定快取有效時間及是否啟用快取
    public class CacheSettings
    {
        public int CacheDurationMinutes { get; set; } = 5;  // 預設快取時間為5分鐘
        public bool CacheEnable { get; set; }               // 是否啟用快取功能
    }
}
