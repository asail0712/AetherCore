namespace AetherCore.Utility.Databases
{
    // 資料庫設定介面，包含連線字串與資料庫名稱
    public interface IDBSetting
    {
        string ConnectionString { get; set; }   // 資料庫連線字串
        string DatabaseName { get; set; }       // 資料庫名稱        
    }
}
