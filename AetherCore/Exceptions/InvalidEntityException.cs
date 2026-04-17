using AetherCore.Utility.Exceptions;

namespace AetherCore.Exceptions
{
    // 無效的實體例外
    public class InvalidEntityException : CustomException
    {
        public InvalidEntityException(string entityName)
            : base($"Invalid or null entity of type '{entityName}' encountered.")
        { }
    }
}
