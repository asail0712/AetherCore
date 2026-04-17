using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace AetherCore.Utility.Convention
{
    // ED TODO 已經棄用
    public sealed class DisabledApiMetadata { }

    public class ListDisableApiConvention : IApplicationModelConvention
    {
        protected virtual HashSet<string> GetDisabledList()
            => new(StringComparer.Ordinal);

        public void Apply(ApplicationModel application)
        {
            var disabled = GetDisabledList();

            foreach (var controller in application.Controllers)
            {
                var controllerName  = controller.ControllerName;
                var wildcardKey     = $"{controllerName}Controller.*";

                foreach (var action in controller.Actions)
                {
                    var key = $"{controllerName}Controller.{action.ActionName}";

                    if (!disabled.Contains(key) && !disabled.Contains(wildcardKey))
                        continue;

                    foreach (var selector in action.Selectors)
                        selector.EndpointMetadata.Add(new DisabledApiMetadata());
                }
            }
        }
    }
}