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
            _baseUrl = baseUrl.TrimEnd('/');
        }

        public async Task<bool> CheckHealthAsync()
        {
            try
            {
                var resp = await _httpClient.GetAsync($"{_baseUrl}/health");
                return resp.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<ApiResponse<LoginResult>> LoginAsync(string username, string password, string appKey)
        {
            var data = new { username, password, app_key = appKey };
            return await PostAsync<LoginResult>("/login", data);
        }

        public async Task<ApiResponse<MarketInfo[]>> GetMarketsAsync(string mode = "rest")
        {
            return await GetAsync<MarketInfo[]>($"/data/markets?mode={mode}");
        }

        public async Task<ApiResponse<MarketInfo[]>> GetHorseRacingMarketsAsync()
        {
            return await GetAsync<MarketInfo[]>("/data/horse-markets");
        }

        public async Task<ApiResponse<T>> GetAsync<T>(string endpoint)
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
            string json = JsonConvert.SerializeObject(data);
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
        [JsonProperty("markets")] public T Markets { get; set; }
    }

    public class LoginResult
    {
        [JsonProperty("session_token")]
        public string SessionToken { get; set; }
        [JsonProperty("message")]
        public string Message { get; set; }
    }

    public class MarketInfo
    {
        [JsonProperty("market_id")]
        public string MarketId { get; set; }

        [JsonProperty("market_name")]
        public string MarketName { get; set; }

        [JsonProperty("start_time")]
        public DateTime StartTime { get; set; }

        [JsonProperty("total_matched")]
        public double TotalMatched { get; set; }
    }
}
