using AetherCore.Utility.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AetherCore.Utility.Convention
{
    public sealed class AuthorizeConvention : IApplicationModelConvention
    {
        public void Apply(ApplicationModel application)
        {
            foreach (var controller in application.Controllers)
            {
                var controllerType = controller.ControllerType.AsType();

                // 只處理有掛 [CrudAuthorize] 的 Controller
                var crudAuthorizeAttr = controllerType
                    .GetCustomAttributes(typeof(CrudAuthorizeAttribute), inherit: true)
                    .FirstOrDefault() as CrudAuthorizeAttribute;

                if (crudAuthorizeAttr == null)
                    continue;

                // 可選：只處理繼承 GenericController 的類別
                if (!IsGenericControllerDerived(controllerType))
                    continue;

                foreach (var action in controller.Actions)
                {
                    var schemes = crudAuthorizeAttr.GetSchemesByActionName(action.ActionName);

                    if (schemes.Length == 0)
                        continue;

                    // 若 action 已經自己有 [AllowAnonymous]，就不要套授權
                    bool hasAllowAnonymous = action.Filters.OfType<AllowAnonymousFilter>().Any()
                        || action.Attributes.OfType<AllowAnonymousAttribute>().Any();

                    if (hasAllowAnonymous)
                        continue;

                    // 若 action 已經有 [Authorize]，可視需求決定是否跳過或覆蓋
                    bool alreadyHasAuthorize =
                        action.Filters.OfType<AuthorizeFilter>().Any() ||
                        action.Attributes.OfType<AuthorizeAttribute>().Any();

                    if (alreadyHasAuthorize)
                        continue;


                    // 加 AuthorizeFilter
                    action.Filters.Add(BuildAuthorizeFilter(schemes));

                    // 再加一份 AuthorizeAttribute 到 EndpointMetadata，給 Swagger 顯示鎖頭
                    var attr = new AuthorizeAttribute
                    {
                        AuthenticationSchemes = string.Join(",", schemes)
                    };

                    foreach (var selector in action.Selectors)
                    {
                        selector.EndpointMetadata.Add(attr);
                    }
                }
            }
        }

        private static AuthorizeFilter BuildAuthorizeFilter(string[] schemes)
        {
            if (schemes == null || schemes.Length == 0)
                return new AuthorizeFilter();

            var policy = new AuthorizationPolicyBuilder()
                .AddAuthenticationSchemes(schemes)
                .RequireAuthenticatedUser()
                .Build();

            return new AuthorizeFilter(policy);
        }

        private static bool IsGenericControllerDerived(Type type)
        {
            while (type != null && type != typeof(object))
            {
                if (type.IsGenericType &&
                    type.GetGenericTypeDefinition().Name == "GenericController`3")
                {
                    return true;
                }

                type = type.BaseType!;
            }

            return false;
        }
    }
}
