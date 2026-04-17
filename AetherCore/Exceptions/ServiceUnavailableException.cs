using AetherCore.Utility.Exceptions;

namespace AetherCore.Exceptions
{
    public class ServiceUnavailableException : CustomException
    {
        public ServiceUnavailableException(string reason)
            : base("Service is temporarily unavailable. Please try again later.")
        {
            // reason 只用來 log，不顯示給 client
            Reason = reason;
        }
        public string Reason { get; }
    }
}
