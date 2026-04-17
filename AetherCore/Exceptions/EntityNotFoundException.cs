using AetherCore.Utility.Exceptions;

namespace AetherCore.Exceptions
{
    // 資料不存在例外
    public class EntityNotFoundException : CustomException
    {
        public EntityNotFoundException(string entityName, string key)
            : base($"{entityName} with key '{key}' was not found.") { }
    }
}
