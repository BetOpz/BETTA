using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BETTA.Services
{
    public class ApiClient
    {
        private readonly HttpClient _httpClient;
        private string _appKey;

        public ApiClient(string baseUrl)
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(baseUrl)          // http://127.0.0.1:5000
            };
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public void SetAppKey(string appKey)
        {
            _appKey = appKey;
            if (_httpClient.DefaultRequestHeaders.Contains("X-AppKey"))
                _httpClient.DefaultRequestHeaders.Remove("X-AppKey");
            _httpClient.DefaultRequestHeaders.Add("X-AppKey", appKey);
        }

        public async Task<LoginResult> LoginAsync(string username, string password)
        {
            // field names must match Flask: username, password, app_key
            var payload = new { username, password, app_key = _appKey };

            var content = new StringContent(
                JsonConvert.SerializeObject(payload),
                Encoding.UTF8,
                "application/json");

            // Flask route is /login
            var response = await _httpClient.PostAsync("/login", content);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<LoginResult>(json);
        }
    }

    public class LoginResult
    {
        [JsonProperty("session_token")]
        public string SessionToken { get; set; }

        [JsonProperty("success")]
        public bool Success { get; set; }
    }
}
