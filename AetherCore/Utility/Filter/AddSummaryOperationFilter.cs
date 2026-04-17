using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using AetherCore.Utility.Attributes;

namespace AetherCore.Utility.Filter
{
    public class AddSummaryOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            if (context.ApiDescription.ActionDescriptor is not ControllerActionDescriptor controllerActionDescriptor)
                return;

            // 1. 先讀取 Method 上的 [CommonSummary]
            var commonSummaryAttr = controllerActionDescriptor.MethodInfo
                .GetCustomAttributes(typeof(CommonSummaryAttribute), inherit: true)
                .FirstOrDefault() as CommonSummaryAttribute;

            if (commonSummaryAttr != null && !string.IsNullOrWhiteSpace(commonSummaryAttr.SummaryContent))
            {
                operation.Summary = commonSummaryAttr.SummaryContent;
                return;
            }

            // 2. 如果沒有 CommonSummary，再走 Controller 上的 CRUD 自動摘要
            var crudResourceAttr = controllerActionDescriptor.ControllerTypeInfo
                .GetCustomAttributes(typeof(CrudSummaryAttribute), inherit: true)
                .FirstOrDefault() as CrudSummaryAttribute;

            if (crudResourceAttr == null)
                return;

            string summaryName  = crudResourceAttr.SummaryTitle;
            string actionName   = controllerActionDescriptor.ActionName;

            operation.Summary = actionName switch
            {
                "Create"    => $"創建{summaryName}資訊",
                "GetAll"    => $"取得所有{summaryName}資訊",
                "Get"       => $"取得指定{summaryName}資訊",
                "Update"    => $"更新指定{summaryName}資訊",
                "Delete"    => $"刪除指定{summaryName}資訊",
                _ => operation.Summary
            };
        }
    }
}