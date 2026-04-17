using AetherCore.Utility.Exceptions;

namespace AetherCore.Exceptions
{
    // 無效的儲存庫參數例外
    public class InvalidRepositoryArgumentException : CustomException
    {
        public InvalidRepositoryArgumentException(string parameterName, string reason)
            : base($"Invalid argument '{parameterName}': {reason}.")
        { }
    }
}
