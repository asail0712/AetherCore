using System.Net.Http.Headers;
using System.Text.Json;

using AetherCore.Auth.LineLogin;

namespace AetherCore.Utility.Auth.Line
{
    public static class LineApiHelper
    {
        public static async Task<LineVerifyResponse> VerifyAccessTokenAsync(HttpClient client, string accessToken)
        {
            var url             = $"https://api.line.me/oauth2/v2.1/verify?access_token={Uri.EscapeDataString(accessToken)}";
            using var response  = await client.GetAsync(url);

            var content         = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"LINE token 驗證失敗: {content}");

            var result = JsonSerializer.Deserialize<LineVerifyResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result == null)
                throw new Exception("LINE token 驗證回傳內容解析失敗");

            return result;
        }

        public static async Task<LineProfileResponse> GetProfileAsync(HttpClient client, string accessToken)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response  = await client.GetAsync("https://api.line.me/v2/profile");
            var content         = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"取得 LINE profile 失敗: {content}");

            var result = JsonSerializer.Deserialize<LineProfileResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result == null)
                throw new Exception("LINE profile 回傳內容解析失敗");

            return result;
        }
    }
}
