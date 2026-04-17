using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

using System.Text;
using AetherCore.Utility.Filter;

namespace AetherCore.Utility
{
    /// <summary>
    /// JWT 相關設定
    /// </summary>
    public class JwtOptions
    {
        public string JwtName { get; set; }                 = String.Empty; // JWT 名稱
        public bool ValidateIssuer { get; set; }            = true;     // 驗證簽發者
        public bool ValidateAudience { get; set; }          = true;     // 驗證接收者
        public bool ValidateLifetime { get; set; }          = true;     // 驗證有效期限
        public bool ValidateIssuerSigningKey { get; set; }  = true;     // 驗證簽章金鑰
        public string Issuer { get; set; }                  = "";       // 簽發者
        public string Audience { get; set; }                = "";       // 接收者
        public string Secret { get; set; }                  = "";       // 秘密金鑰
    }

    public static class JwtExtensions
    {
        /// <summary>
        /// 設定 JWT 認證服務
        /// </summary>
        public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, List<JwtOptions> jwtOptions)
        {
            if (jwtOptions == null || jwtOptions.Count == 0)
                throw new ArgumentException("jwtOptions is empty.");

            // 注入 JwtOptions
            foreach (JwtOptions option in jwtOptions)
            {
                if (string.IsNullOrWhiteSpace(option.JwtName))
                    throw new ArgumentException("JwtName is required.");

                // 也可以加基本檢查，避免空字串炸在 runtime
                if (string.IsNullOrWhiteSpace(option.Issuer) ||
                    string.IsNullOrWhiteSpace(option.Audience) ||
                    string.IsNullOrWhiteSpace(option.Secret))
                    throw new ArgumentException($"JwtOptions({option.JwtName}) Issuer/Audience/Secret is empty.");

                services.Configure<JwtOptions>(option.JwtName, o =>
                {
                    o.ValidateIssuer            = option.ValidateIssuer;
                    o.ValidateAudience          = option.ValidateAudience;
                    o.ValidateLifetime          = option.ValidateLifetime;
                    o.ValidateIssuerSigningKey  = option.ValidateIssuerSigningKey;
                    o.Issuer                    = option.Issuer;
                    o.Audience                  = option.Audience;
                    o.Secret                    = option.Secret;
                });
            }

            // 設定 default authentication scheme
            var defaultScheme   = jwtOptions[0].JwtName;
            var authBuilder     = services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme   = defaultScheme;
                options.DefaultChallengeScheme      = defaultScheme;
            });

            // 加上 JwtBearer Handler 
            foreach (var option in jwtOptions)
            {
                authBuilder.AddJwtBearer(option.JwtName, _ => { });

                // 在第一次Authorize時 使用注入的jwt option 配置 JwtBearerOptions
                services.AddOptions<JwtBearerOptions>(option.JwtName)
                    .Configure<IOptionsMonitor<JwtOptions>>((options, monitor) =>
                    {
                        var jwt = monitor.Get(option.JwtName);

                        options.TokenValidationParameters = new TokenValidationParameters
                        {
                            ValidateIssuer              = jwt.ValidateIssuer,
                            ValidateAudience            = jwt.ValidateAudience,
                            ValidateLifetime            = jwt.ValidateLifetime,
                            ValidateIssuerSigningKey    = jwt.ValidateIssuerSigningKey,

                            ValidIssuer                 = jwt.Issuer,
                            ValidAudience               = jwt.Audience,
                            IssuerSigningKey            = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
                            ClockSkew                   = TimeSpan.FromMinutes(1)
                        };
                    });
            }

            return services;
        }
    }
}
