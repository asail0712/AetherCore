using AetherCore.Module.Token.Interface;
using AetherCore.Utility;
using AetherCore.Utility.Attributes;
using AetherCore.Utility.JWT;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AetherCore.Module.Token
{
    public class JWTokenServiceFactory : ITokenServiceFactory
    {
        private IOptionsMonitor<JwtOptions> _jwtOptionsMonitor;
        public JWTokenServiceFactory(IOptionsMonitor<JwtOptions> jwtOptionsMonitor)
        {
            _jwtOptionsMonitor = jwtOptionsMonitor;
        }
        
        public ITokenService Create(string optionName)
        {
            JwtOptions jwtOptions = _jwtOptionsMonitor.Get(optionName); // 確保有對應的設定存在

            return new JwtGenerator(jwtOptions.Secret, jwtOptions.Issuer, jwtOptions.Audience);
        }
    }
}
