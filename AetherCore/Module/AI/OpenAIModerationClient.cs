using AetherCore.DTO;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AetherCore.Settings;
using AetherCore.Module.Interface;

namespace AetherCore.Module.AI
{
    internal class OpenAIModerationApiResponse
    {
        public List<OpenAIModerationResult> results { get; set; } = new();
    }

    internal class OpenAIModerationResult
    {
        public bool flagged { get; set; }
        public Dictionary<string, bool> categories { get; set; } = new();
        public Dictionary<string, double> category_scores { get; set; } = new();
    }

    public class OpenAIModerationClient : IModerationClient
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;

        public OpenAIModerationClient(HttpClient http, IOptions<OpenAISettings> opt)
        {
            _http   = http;
            _apiKey = opt.Value.ApiKey;
        }

        public async Task<List<ModerationResponse>> CheckBatchAsync(List<ModerationInput> inputs)
        {
            if (inputs == null || inputs.Count == 0)
                return new List<ModerationResponse>();

            // OpenAI moderation API 不認 MessageId
            // 先用 index 對齊，回來再補 MessageId
            var requestBody = new
            {
                model = "omni-moderation-latest",
                input = inputs.Select(i => i.Content).ToList()
            };

            var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://api.openai.com/v1/moderations"
            );

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _apiKey);

            request.Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json"
            );

            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json    = await response.Content.ReadAsStringAsync();
            var raw     = JsonSerializer.Deserialize<OpenAIModerationApiResponse>(json);

            // 對齊 MessageId
            var resultItems = raw.results
                .Select((r, index) => new ModerationResultItem
                {
                    MessageId   = inputs[index].MessageId,
                    Result      = new ModerationCheckResult
                    {
                        Flagged         = r.flagged,
                        Categories      = r.categories,
                        CategoryScores  = r.category_scores
                    }
                })
                .ToList();

            return new List<ModerationResponse>
            {
                new ModerationResponse
                {
                    Results = resultItems
                }
            };
        }
    }
}