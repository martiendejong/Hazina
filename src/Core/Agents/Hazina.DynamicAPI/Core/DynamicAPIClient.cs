using System.Text;
using System.Text.Json;
using Hazina.DynamicAPI.Models;

namespace Hazina.DynamicAPI.Core;

/// <summary>
/// Dynamic HTTP client that can call any API endpoint based on discovered documentation
/// </summary>
public class DynamicAPIClient
{
    private readonly CredentialStore _credentialStore;
    private readonly HttpClient _httpClient;

    public DynamicAPIClient(CredentialStore credentialStore, HttpClient? httpClient = null)
    {
        _credentialStore = credentialStore;
        _httpClient = httpClient ?? new HttpClient();
    }

    /// <summary>
    /// Execute an API request with automatic credential injection
    /// </summary>
    public async Task<ApiResponse> ExecuteRequest(ApiRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            // Build the URL with query parameters
            var url = BuildUrl(request.Url, request.QueryParameters);

            // Create HTTP request
            var httpRequest = new HttpRequestMessage(
                new HttpMethod(request.Method.ToUpper()),
                url
            );

            // Add headers
            if (request.Headers != null)
            {
                foreach (var header in request.Headers)
                {
                    httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            // Add authentication
            await AddAuthentication(httpRequest, request);

            // Add body if present
            if (request.Body != null && (request.Method.ToUpper() == "POST" || request.Method.ToUpper() == "PUT" || request.Method.ToUpper() == "PATCH"))
            {
                var bodyJson = request.Body is string str
                    ? str
                    : JsonSerializer.Serialize(request.Body);

                httpRequest.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
            }

            // Execute request
            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

            // Build response
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            var apiResponse = new ApiResponse
            {
                StatusCode = (int)response.StatusCode,
                Body = responseBody,
                IsSuccess = response.IsSuccessStatusCode,
                Headers = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value))
            };

            if (!response.IsSuccessStatusCode)
            {
                apiResponse.ErrorMessage = $"HTTP {response.StatusCode}: {response.ReasonPhrase}";
            }

            return apiResponse;
        }
        catch (Exception ex)
        {
            return new ApiResponse
            {
                StatusCode = 0,
                IsSuccess = false,
                ErrorMessage = $"Request failed: {ex.Message}",
                Body = ex.ToString()
            };
        }
    }

    /// <summary>
    /// Execute a GET request
    /// </summary>
    public async Task<ApiResponse> Get(string url, string? serviceName = null, Dictionary<string, string>? queryParams = null)
    {
        return await ExecuteRequest(new ApiRequest
        {
            Url = url,
            Method = "GET",
            ServiceName = serviceName,
            QueryParameters = queryParams,
            AuthType = string.IsNullOrEmpty(serviceName) ? AuthenticationType.None : AuthenticationType.Bearer
        });
    }

    /// <summary>
    /// Execute a POST request
    /// </summary>
    public async Task<ApiResponse> Post(string url, object body, string? serviceName = null)
    {
        return await ExecuteRequest(new ApiRequest
        {
            Url = url,
            Method = "POST",
            ServiceName = serviceName,
            Body = body,
            AuthType = string.IsNullOrEmpty(serviceName) ? AuthenticationType.None : AuthenticationType.Bearer
        });
    }

    /// <summary>
    /// Execute a PUT request
    /// </summary>
    public async Task<ApiResponse> Put(string url, object body, string? serviceName = null)
    {
        return await ExecuteRequest(new ApiRequest
        {
            Url = url,
            Method = "PUT",
            ServiceName = serviceName,
            Body = body,
            AuthType = string.IsNullOrEmpty(serviceName) ? AuthenticationType.None : AuthenticationType.Bearer
        });
    }

    /// <summary>
    /// Execute a DELETE request
    /// </summary>
    public async Task<ApiResponse> Delete(string url, string? serviceName = null)
    {
        return await ExecuteRequest(new ApiRequest
        {
            Url = url,
            Method = "DELETE",
            ServiceName = serviceName,
            AuthType = string.IsNullOrEmpty(serviceName) ? AuthenticationType.None : AuthenticationType.Bearer
        });
    }

    private async Task AddAuthentication(HttpRequestMessage request, ApiRequest apiRequest)
    {
        if (apiRequest.AuthType == AuthenticationType.None || string.IsNullOrEmpty(apiRequest.ServiceName))
        {
            return;
        }

        try
        {
            switch (apiRequest.AuthType)
            {
                case AuthenticationType.Bearer:
                    var bearerToken = await _credentialStore.GetCredential(apiRequest.ServiceName, "api_key");
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);
                    break;

                case AuthenticationType.ApiKey:
                    var apiKey = await _credentialStore.GetCredential(apiRequest.ServiceName, "api_key");
                    var apiKeyHeader = await _credentialStore.GetCredential(apiRequest.ServiceName, "api_key_header");
                    request.Headers.TryAddWithoutValidation(apiKeyHeader, apiKey);
                    break;

                case AuthenticationType.Basic:
                    var username = await _credentialStore.GetCredential(apiRequest.ServiceName, "username");
                    var password = await _credentialStore.GetCredential(apiRequest.ServiceName, "password");
                    var basicAuth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", basicAuth);
                    break;

                case AuthenticationType.OAuth2:
                    var accessToken = await _credentialStore.GetCredential(apiRequest.ServiceName, "access_token");
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                    break;
            }
        }
        catch (CredentialNotFoundException ex)
        {
            Console.WriteLine($"Warning: {ex.Message}. Proceeding without authentication.");
        }
    }

    private string BuildUrl(string baseUrl, Dictionary<string, string>? queryParameters)
    {
        if (queryParameters == null || queryParameters.Count == 0)
        {
            return baseUrl;
        }

        var queryString = string.Join("&", queryParameters.Select(kvp =>
            $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"
        ));

        var separator = baseUrl.Contains('?') ? "&" : "?";
        return $"{baseUrl}{separator}{queryString}";
    }
}
