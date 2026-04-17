using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace AetherCore.Utility.Filter
{
    /// <summary>
    /// 依據 EndpointMetadata 的授權資訊（Authorize/AllowAnonymous）自動加 Swagger 鎖頭。
    /// 搭配 ListAuthorizeConvention：不需再維護第二份字典。
    /// </summary>
    public sealed class EndpointMetadataAuthorizeLockFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var metadata = context.ApiDescription?.ActionDescriptor?.EndpointMetadata;
            if (metadata == null) return;

            // 有 AllowAnonymous → 不加鎖頭
            if (metadata.OfType<IAllowAnonymous>().Any() ||
                metadata.OfType<AllowAnonymousAttribute>().Any())
                return;

            // 找出授權資料（這裡會包含你 Convention 加上的 Authorize）
            var authData = metadata.OfType<IAuthorizeData>().ToList();
            if (authData.Count == 0) return;

            // 收集 schemes（可能為 "AppJwt,AdminJwt"）
            var schemes = authData
                .SelectMany(a => (a.AuthenticationSchemes ?? "")
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (schemes.Count == 0)
                return;

            operation.Security ??= new List<OpenApiSecurityRequirement>();

            foreach (var schemeName in schemes)
            {
                var scheme = new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type    = ReferenceType.SecurityScheme,
                        Id      = schemeName
                    }
                };

                operation.Security.Add(new OpenApiSecurityRequirement
                {
                    [scheme] = Array.Empty<string>()
                });
            }
        }
    }
}
