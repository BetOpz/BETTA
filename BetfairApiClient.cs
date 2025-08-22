using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BETTA
{
    public class BetfairApiClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public BetfairApiClient(string baseUrl = "http://127.0.0.1:5000")
        {
            _httpClient = new HttpClient();
            _baseUrl = baseUrl;
        }

        public async Task<ApiResponse<LoginResult>> LoginAsync(string username, string password, string appKey)
        {
            var loginData = new { username, password, app_key = appKey };
            return await PostAsync<LoginResult>("/login", loginData);
        }

        public async Task<ApiResponse<object>> LogoutAsync() => await PostAsync<object>("/logout", null);
        public async Task<ApiResponse<AccountInfo>> GetAccountInfoAsync() => await GetAsync<AccountInfo>("/account");
        public async Task<ApiResponse<MarketsResult>> GetMarketsAsync() => await GetAsync<MarketsResult>("/markets");
        public async Task<ApiResponse<ServiceStatus>> GetStatusAsync() => await GetAsync<ServiceStatus>("/status");
        public async Task<bool> CheckHealthAsync()
        {
            try
            {
                var resp = await _httpClient.GetAsync($"{_baseUrl}/health");
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        private async Task<ApiResponse<T>> GetAsync<T>(string endpoint)
        {
            try
            {
                var resp = await _httpClient.GetAsync(_baseUrl + endpoint);
                var json = await resp.Content.ReadAsStringAsync();
                Debug.WriteLine($"GET {endpoint} response: {json}");
                return JsonConvert.DeserializeObject<ApiResponse<T>>(json);
            }
            catch (Exception ex)
            {
                return new ApiResponse<T> { Success = false, Error = ex.Message };
            }
        }

        private async Task<ApiResponse<T>> PostAsync<T>(string endpoint, object data)
        {
            string json = JsonConvert.SerializeObject(data ?? new { });
            Debug.WriteLine($"POST {endpoint} payload: {json}");
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = await _httpClient.PostAsync(_baseUrl + endpoint, content);
            var respJson = await resp.Content.ReadAsStringAsync();
            Debug.WriteLine($"POST {endpoint} response: {respJson}");
            return JsonConvert.DeserializeObject<ApiResponse<T>>(respJson);
        }

        public void Dispose() => _httpClient.Dispose();
    }

    public class ApiResponse<T>
    {
        [JsonProperty("success")] public bool Success { get; set; }
        [JsonProperty("error")] public string Error { get; set; }
        [JsonProperty("session_token")] public string SessionToken { get; set; }
        [JsonProperty("message")] public string Message { get; set; }
        [JsonProperty("account")] public T Account { get; set; }
        [JsonProperty("markets")] public T Markets { get; set; }
    }

    public class LoginResult { public string SessionToken { get; set; } public string Message { get; set; } }
    public class AccountInfo
    {
        public string Currency { get; set; }
        public string Firstname { get; set; }
        public string Lastname { get; set; }
        public double AvailableBalance { get; set; }
        public double Exposure { get; set; }
        public double RetainedCommission { get; set; }
    }
    public class MarketsResult { public MarketInfo[] Markets { get; set; } }
    public class MarketInfo { public string Id { get; set; } public string Name { get; set; } public int MarketCount { get; set; } }
    public class ServiceStatus
    {
        [JsonProperty("logged_in")] public bool LoggedIn { get; set; }
        [JsonProperty("session_token")] public string SessionToken { get; set; }
        [JsonProperty("keep_alive_active")] public bool KeepAliveActive { get; set; }
    }
}
