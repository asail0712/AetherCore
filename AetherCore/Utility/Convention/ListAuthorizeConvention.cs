using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;
using System.Data;

namespace AetherCore.Utility.Convention
{
    // ED TODO 已經棄用

    public class ListAuthorizeConvention : IApplicationModelConvention
    {
        // 可覆寫方法，回傳 Action 名稱與 Summary 內容的對應字典
        protected virtual Dictionary<string, string> GetAuthorizeInfo()
        {
            // 預設回傳空字典，子類別覆寫填入內容
            return new Dictionary<string, string>();
        }

        public void Apply(ApplicationModel application)
        {
            Dictionary<string, string> authorizeInfo = GetAuthorizeInfo();

            foreach (var controller in application.Controllers)
            {
                var controllerName  = controller.ControllerName; // e.g. "Auth"
                var wildcardKey     = $"{controllerName}Controller.*";

                foreach (var action in controller.Actions)
                {
                    // 如果 action 本身有 [AllowAnonymous]，就不要套用
                    if (action.Attributes.OfType<AllowAnonymousAttribute>().Any() ||
                        action.Filters.OfType<AllowAnonymousFilter>().Any())
                        continue;

                    // 組出你現在習慣的 key："{Controller}Controller.{Action}"
                    var key = $"{controllerName}Controller.{action.ActionName}";

                    // 先找精準 key，再找 wildcard key
                    if (!authorizeInfo.TryGetValue(key, out var schemes) &&
                        !authorizeInfo.TryGetValue(wildcardKey, out schemes))
                    {
                        // 沒有規則就不套（你也可以改成：沒有規則就預設套）
                        continue;
                    }

                    // 加 Authorize
                    action.Filters.Add(BuildAuthorizeFilter(schemes));
                    // ★ 再加一份 AuthorizeAttribute 到 EndpointMetadata（給 Swagger 讀鎖頭）
                    var attr = new AuthorizeAttribute();
                    if (!string.IsNullOrWhiteSpace(schemes))
                        attr.AuthenticationSchemes = schemes;

                    foreach (var selector in action.Selectors)
                    {
                        selector.EndpointMetadata.Add(attr);
                    }
                }
            }
        }

        private static AuthorizeFilter BuildAuthorizeFilter(string? schemes)
        {
            // 沒 schemes：等同 [Authorize]
            if (string.IsNullOrWhiteSpace(schemes))
                return new AuthorizeFilter();

            var schemeList  = schemes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var policy      = new AuthorizationPolicyBuilder()
                .AddAuthenticationSchemes(schemeList)
                .RequireAuthenticatedUser()
                .Build();

            return new AuthorizeFilter(policy);
        }
    }
}