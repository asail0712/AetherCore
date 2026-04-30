namespace AetherCore.Utility.Databases
{
    // SQL 資料庫連線設定，實作 IDBSetting 介面
    public class SqlDBSettings : IDBSetting
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;
    }
}
