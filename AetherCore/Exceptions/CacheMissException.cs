using AetherCore.Utility.Exceptions;

namespace AetherCore.Exceptions
{
    // 快取未命中例外
    public class CacheMissException : CustomException
    {
        public CacheMissException(string cacheKey)
            : base($"Cache miss for key '{cacheKey}'.") { }
    }
}
