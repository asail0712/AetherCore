using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace AetherCore.Utility.Filter
{
    // ED TODO 已經棄用
    // 用於 Swagger 文件中隱藏特定 API 的 DocumentFilter
    public class HiddenApiDocumentFilter : IDocumentFilter
    {
        // 可覆寫取得欲隱藏的 API 清單（格式為 ControllerName.MethodName）
        protected virtual HashSet<string> GetHiddenList()
        {
            return new HashSet<string>();
        }

        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            var closedApis      = GetHiddenList();
            var pathsToRemove   = new List<string>();

            // 逐一檢查 swaggerDoc 中的路徑
            foreach (var path in swaggerDoc.Paths)
            {
                var operationsToRemove = new List<OperationType>();

                // 逐一檢查該路徑下的 HTTP 操作 (GET, POST...)
                foreach (var operation in path.Value.Operations)
                {
                    // 取得對應的 API 描述
                    var apiDesc = context.ApiDescriptions.FirstOrDefault(d =>
                        d.RelativePath!.Equals(path.Key.TrimStart('/'), StringComparison.OrdinalIgnoreCase)
                        && d.HttpMethod!.Equals(operation.Key.ToString(), StringComparison.OrdinalIgnoreCase));

                    if (apiDesc != null)
                    {
                        // 取得 Controller 名稱（加上 Controller 後綴）
                        var controllerName  = apiDesc.ActionDescriptor.RouteValues["controller"] + "Controller";
                        // 取得方法名稱
                        var methodName      = (apiDesc.ActionDescriptor as ControllerActionDescriptor)?.MethodInfo.Name;
                        // 組合完整方法名稱字串
                        var fullMethodName  = $"{controllerName}.{methodName}";

                        // 如果該方法名稱在隱藏清單中，標記該 Operation 待移除
                        if (closedApis.Contains(fullMethodName))
                        {
                            operationsToRemove.Add(operation.Key);
                        }
                    }
                }

                // 移除指定要隱藏的 Operation
                foreach (var op in operationsToRemove)
                {
                    path.Value.Operations.Remove(op);
                }

                // 如果該路徑沒有任何 Operation，則整條路徑標記為待移除
                if (!path.Value.Operations.Any())
                {
                    pathsToRemove.Add(path.Key);
                }
            }

            // 移除所有無 Operation 的路徑
            foreach (var pathKey in pathsToRemove)
            {
                swaggerDoc.Paths.Remove(pathKey);
            }
        }
    }
}
