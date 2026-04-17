namespace AetherCore.Utility.Databases
{
    // MongoDB 連線設定，實作 IDBSetting 介面
    public class MongoDBSettings : IDBSetting
    {
        public string ConnectionString { get; set; }  // MongoDB 連線字串
        public string DatabaseName { get; set; }      // 資料庫名稱
    }
}
