using System.Net.Http.Json;

namespace AetherCore.Utility.Lincense
{
    internal static class XPlanLicenseRuntime
    {
        private static readonly SemaphoreSlim _lock = new(1, 1);

        private static bool? _isValid;
        private static DateTime _nextRefreshUtc = DateTime.MinValue;

        private static string? _endpoint;
        private static string? _licenseKey;

        private static TimeSpan RefreshInterval         = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan HttpTimeout    = TimeSpan.FromSeconds(5);

        private static readonly HttpClient _httpClient  = new()
        {
            Timeout = HttpTimeout
        };

        public static async Task EnsureValidOrThrowAsync()
        {
            if (_isValid == true && DateTime.UtcNow < _nextRefreshUtc)
                return;

            await _lock.WaitAsync();
            try
            {
                // double check (避免多執行緒重複打 API)
                if (_isValid == true && DateTime.UtcNow < _nextRefreshUtc)
                    return;

                LoadConfigIfNeeded();

                if (string.IsNullOrWhiteSpace(_endpoint) ||
                    string.IsNullOrWhiteSpace(_licenseKey))
                {
                    throw new InvalidOperationException(
                        "XPlan License configuration missing. " +
                        "Please set XPLAN_LICENSE_ENDPOINT and XPLAN_LICENSE_KEY.");
                }

                var request = new ValidateRequest
                {
                    Key = _licenseKey!
                };

                HttpResponseMessage response;

                try
                {
                    response = await _httpClient.PostAsJsonAsync(_endpoint, request);
                }
                catch (Exception ex)
                {
                    // Fail-Close 策略
                    _isValid = false;
                    throw new InvalidOperationException("License server unreachable.", ex);
                }

                if (!response.IsSuccessStatusCode)
                {
                    _isValid = false;
                    throw new InvalidOperationException(
                        $"License validation failed. StatusCode={response.StatusCode}");
                }

                var result = await response.Content.ReadFromJsonAsync<ValidateResponse>();

                if (result == null || result.Valid != true)
                {
                    _isValid = false;
                    throw new UnauthorizedAccessException("License invalid.");
                }

                // 驗證成功
                int randomMin   = Random.Shared.Next(15, 45);
                RefreshInterval = TimeSpan.FromMinutes(randomMin);
                _isValid        = true;
                _nextRefreshUtc = DateTime.UtcNow.Add(RefreshInterval);
            }
            finally
            {
                _lock.Release();
            }
        }

        private static void LoadConfigIfNeeded()
        {
            if (_endpoint != null)
                return;

            //var config = new ConfigurationBuilder()
            //    .SetBasePath(AppContext.BaseDirectory)
            //    .AddJsonFile("appsettings.json", optional: true)
            //    .AddJsonFile(
            //        $"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")}.json",
            //        optional: true)
            //    .AddEnvironmentVariables()
            //    .Build();

            //_endpoint   = config["XPlanLicense:Endpoint"];
            //_licenseKey = config["XPlanLicense:Key"];
            _endpoint   = "https://licenseserver-b9f3haedbmesbyfz.southeastasia-01.azurewebsites.net/v1/validate";
            _licenseKey = "XPLAN_Db-mv65R1u52s3OZNSbrPc1Pfx85SFSe3VaLhfLrSsw";
        }

        private class ValidateRequest
        {
            public string Key { get; set; } = string.Empty;
        }

        private class ValidateResponse
        {
            public bool Valid { get; set; }
        }
    }
}
