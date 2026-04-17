using AetherCore.Utility.Exceptions;

namespace AetherCore.Exceptions
{
    // 資料庫操作失敗例外
    public class DatabaseOperationException : CustomException
    {
        public DatabaseOperationException(string operation, string entityName, Exception inner)
            : base($"Database operation '{operation}' failed for entity '{entityName}'. Becuz {inner.Message}", inner) { }
    }
}
