using Swashbuckle.AspNetCore.SwaggerGen;
using Microsoft.OpenApi.Models;

namespace AetherCore.Utility.Filter
{
    // ED TODO 已經棄用
    // 自訂 Swagger OperationFilter，用於自動加上 API Summary 說明
    public class AddSummaryFilter : IOperationFilter
    {
        // 實作 Apply 方法，根據 Action 名稱設定對應的 Summary
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            string displayName  = context.ApiDescription!.ActionDescriptor!.DisplayName!; // 取得 API Action 的完整顯示名稱
            var keyValuePairs   = GetSummaryInfo(); // 取得對應 Action 名稱與 Summary 的字典

            foreach (var pair in keyValuePairs)
            {
                // 如果 Action 名稱包含字典中的 Key，則設定 Summary
                if (displayName.Contains("." + pair.Key))
                {
                    operation.Summary = pair.Value;
                    return; // 找到後直接回傳
                }
            }
        }

        // 可覆寫方法，回傳 Action 名稱與 Summary 內容的對應字典
        protected virtual Dictionary<string, string> GetSummaryInfo()
        {
            // 預設回傳空字典，子類別覆寫填入內容
            return new Dictionary<string, string>();
        }
    }
}
