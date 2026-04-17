using AetherCore.Utility.Exceptions;

namespace AetherCore.Exceptions
{
    public class InvalidCredentialsException : CustomException
    {
        public InvalidCredentialsException(string errorMsg = $"Invalid account or password.")
            : base(errorMsg) { }
    }
}
