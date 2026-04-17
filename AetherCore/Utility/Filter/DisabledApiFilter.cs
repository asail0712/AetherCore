using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using AetherCore.Utility.Convention;

namespace AetherCore.Utility.Filter
{
    // ED TODO 已經棄用
    public sealed class DisabledApiFilter : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var disabled = context.ActionDescriptor.EndpointMetadata
                .OfType<DisabledApiMetadata>()
                .Any();

            if (disabled)
            {
                context.Result = new NotFoundResult();
                return;
            }

            await next();
        }
    }
}
