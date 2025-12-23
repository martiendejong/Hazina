using System.Net.Http.Headers;
using System.Text;
using DevGPT.GenerationTools.Data;
using Microsoft.Extensions.Configuration;
using DevGPT.GenerationTools.Services.FileOps.Helpers;
using Polly;
using Polly.Extensions.Http;

namespace DevGPTStore.Services
{
    public class WordpressBaseService
    {
        protected readonly HttpClient _httpClient;
        protected readonly string _baseUrl;
        protected readonly string _apiPrefix;
        protected readonly string _username;
        protected readonly string _password;
        private readonly ResiliencePipeline<HttpResponseMessage> _retryPipeline;

        protected readonly IConfiguration AppConfig;

        public WordpressBaseService(IConfiguration configuration, string baseUrl, string username, string password, HttpClient httpClient = null)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new WordPressConfigurationException("WordPress baseUrl ontbreekt of is leeg.");
            AppConfig = configuration;
            _baseUrl = baseUrl.TrimEnd('/');
            // Allow overriding the WordPress API prefix; default to custom plugin paths
            _apiPrefix = configuration["WordPress:ApiPrefix"] ?? "wp-json/api/v1";
            _username = username ?? throw new WordPressConfigurationException("WordPress username moet gespecificeerd zijn.");
            _password = password ?? throw new WordPressConfigurationException("WordPress password moet gespecificeerd zijn.");

            bool useInsecureClient = false;
            try
            {
                var insecureValue = configuration["UseInsecureWordpressClient"];
                if (!string.IsNullOrWhiteSpace(insecureValue))
                {
                    useInsecureClient = bool.TryParse(insecureValue, out var parsed) ? parsed : (insecureValue == "1");
                }
            }
            catch { }

            _httpClient = httpClient ?? (useInsecureClient ? CreateInsecureClient() : new HttpClient());

            // Configure Polly retry pipeline for HTTP 429 (rate limiting)
            _retryPipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
                .AddRetry(new Polly.Retry.RetryStrategyOptions<HttpResponseMessage>
                {
                    ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                        .Handle<HttpRequestException>()
                        .HandleResult(r => (int)r.StatusCode == 429),
                    MaxRetryAttempts = 5,
                    Delay = TimeSpan.FromSeconds(5),
                    OnRetry = args =>
                    {
                        var retryAfterSeconds = 5;
                        if (args.Outcome.Result?.Headers.TryGetValues("Retry-After", out var values) == true)
                        {
                            var retryAfterStr = values.FirstOrDefault();
                            if (int.TryParse(retryAfterStr, out var parsed))
                                retryAfterSeconds = parsed;
                        }
                        Console.WriteLine($"Received HTTP 429. Retry {args.AttemptNumber + 1}/5. Retrying after {retryAfterSeconds} seconds...");
                        return ValueTask.CompletedTask;
                    }
                })
                .Build();
        }

        private static HttpClient CreateInsecureClient()
        {
            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            return new HttpClient(handler);
        }

        protected HttpRequestMessage CreateHttpRequest(HttpMethod method, string url, HttpContent content = null)
        {
            var request = new HttpRequestMessage(method, url);
            if (content != null)
                request.Content = content;

            string basicAuth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_username}:{_password}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);
            return request;
        }

        protected async Task<HttpResponseMessage> SendWithRetryAsync(HttpRequestMessage request, string context, int maxRetries = 5)
        {
            var response = await _retryPipeline.ExecuteAsync(async ct =>
            {
                var result = await _httpClient.SendAsync(request, ct);
                if (!result.IsSuccessStatusCode && (int)result.StatusCode != 429)
                {
                    Console.WriteLine($"[{context}] Request failed: {result.StatusCode} {result.ReasonPhrase}");
                }
                return result;
            });

            return response;
        }
    }
}
