using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hazina.Tools.Services.AI.Intake
{
    internal class OpenAIJsonClient
    {
        private readonly string _apiKey;
        private readonly string _model;

        public OpenAIJsonClient(string apiKey, string model = "gpt-4o-mini")
        {
            _apiKey = apiKey;
            _model = model;
        }

        public async Task<string> GetJsonAsync(string system, string user)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            var payload = new
            {
                model = _model,
                response_format = new { type = "json_object" },
                messages = new object[]
                {
                    new { role = "system", content = system },
                    new { role = "REGULAR", content = user }
                }
            };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = await client.PostAsync("https://api.openai.com/v1/chat/completions", content);
            resp.EnsureSuccessStatusCode();
            var body = await resp.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var messageContent = root.GetProperty("choices")[0]
                                     .GetProperty("message")
                                     .GetProperty("content")
                                     .GetString();
            return messageContent ?? "{}";
        }
    }
}

