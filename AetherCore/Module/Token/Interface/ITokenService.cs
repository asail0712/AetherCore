using System.Security.Claims;

namespace AetherCore.Module.Token.Interface
{
    public interface ITokenService
    {
        string GenerateToken(string userId, string userName, TimeSpan? lifetime = null, IEnumerable<Claim>? extraClaims = null);
    }
}
