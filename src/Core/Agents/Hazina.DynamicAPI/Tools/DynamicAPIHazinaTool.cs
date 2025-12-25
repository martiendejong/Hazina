using System.Text.Json;
using Hazina.DynamicAPI.Core;
using Hazina.DynamicAPI.Models;

namespace Hazina.DynamicAPI.Tools;

/// <summary>
/// HazinaChatTool wrapper for making dynamic API calls.
/// Allows agents to call any API endpoint without pre-configuration.
/// </summary>
public class DynamicAPIHazinaTool : HazinaChatTool
{
    private readonly DynamicAPIClient _apiClient;

    public DynamicAPIHazinaTool(DynamicAPIClient apiClient) : base(
        "api_call",
        "Make HTTP requests to any API endpoint. Automatically handles authentication if service credentials are available. " +
        "Supports GET, POST, PUT, DELETE, PATCH methods. Returns JSON responses.",
        new List<ChatToolParameter>
        {
            new ChatToolParameter
            {
                Name = "url",
                Description = "The full URL of the API endpoint to call",
                Type = "string",
                Required = true
            },
            new ChatToolParameter
            {
                Name = "method",
                Description = "HTTP method: GET, POST, PUT, DELETE, or PATCH (default: GET)",
                Type = "string",
                Required = false
            },
            new ChatToolParameter
            {
                Name = "service_name",
                Description = "Name of the service for credential lookup (e.g., 'stripe', 'google_analytics'). If provided, automatically adds authentication.",
                Type = "string",
                Required = false
            },
            new ChatToolParameter
            {
                Name = "headers",
                Description = "Optional HTTP headers as a JSON object",
                Type = "object",
                Required = false
            },
            new ChatToolParameter
            {
                Name = "query_params",
                Description = "Optional query parameters as a JSON object",
                Type = "object",
                Required = false
            },
            new ChatToolParameter
            {
                Name = "body",
                Description = "Request body for POST/PUT/PATCH requests as a JSON object or string",
                Type = "object",
                Required = false
            },
            new ChatToolParameter
            {
                Name = "auth_type",
                Description = "Authentication type: none, bearer, api_key, basic, oauth2 (default: bearer if service_name provided)",
                Type = "string",
                Required = false
            }
        },
        async (messages, call, cancel) =>
        {
            try
            {
                using var doc = JsonDocument.Parse(call.FunctionArguments);
                var root = doc.RootElement;

                // Parse required parameters
                if (!root.TryGetProperty("url", out var urlProp))
                {
                    return "Error: 'url' parameter is required";
                }
                var url = urlProp.GetString();
                if (string.IsNullOrEmpty(url))
                {
                    return "Error: 'url' cannot be empty";
                }

                // Parse optional parameters
                var method = root.TryGetProperty("method", out var methodProp)
                    ? methodProp.GetString() ?? "GET"
                    : "GET";

                var serviceName = root.TryGetProperty("service_name", out var serviceProp)
                    ? serviceProp.GetString()
                    : null;

                Dictionary<string, string>? headers = null;
                if (root.TryGetProperty("headers", out var headersProp))
                {
                    headers = JsonSerializer.Deserialize<Dictionary<string, string>>(headersProp.GetRawText());
                }

                Dictionary<string, string>? queryParams = null;
                if (root.TryGetProperty("query_params", out var queryProp))
                {
                    queryParams = JsonSerializer.Deserialize<Dictionary<string, string>>(queryProp.GetRawText());
                }

                object? body = null;
                if (root.TryGetProperty("body", out var bodyProp))
                {
                    body = bodyProp.ValueKind == JsonValueKind.String
                        ? bodyProp.GetString()
                        : JsonSerializer.Deserialize<object>(bodyProp.GetRawText());
                }

                var authTypeStr = root.TryGetProperty("auth_type", out var authProp)
                    ? authProp.GetString()
                    : null;

                var authType = ParseAuthType(authTypeStr, serviceName);

                // Build and execute request
                var request = new ApiRequest
                {
                    Url = url,
                    Method = method,
                    ServiceName = serviceName,
                    Headers = headers,
                    QueryParameters = queryParams,
                    Body = body,
                    AuthType = authType
                };

                var response = await apiClient.ExecuteRequest(request, cancel);

                // Format response
                if (response.IsSuccess)
                {
                    return $"Success (HTTP {response.StatusCode}):\n{response.Body}";
                }
                else
                {
                    return $"Error: {response.ErrorMessage}\nStatus: HTTP {response.StatusCode}\nBody: {response.Body}";
                }
            }
            catch (Exception ex)
            {
                return $"API call failed: {ex.Message}";
            }
        }
    )
    {
        _apiClient = apiClient;
    }

    private static AuthenticationType ParseAuthType(string? authTypeStr, string? serviceName)
    {
        if (string.IsNullOrEmpty(authTypeStr))
        {
            // Default: use bearer if service name is provided
            return string.IsNullOrEmpty(serviceName)
                ? AuthenticationType.None
                : AuthenticationType.Bearer;
        }

        return authTypeStr.ToLower() switch
        {
            "bearer" => AuthenticationType.Bearer,
            "api_key" => AuthenticationType.ApiKey,
            "basic" => AuthenticationType.Basic,
            "oauth2" => AuthenticationType.OAuth2,
            "none" => AuthenticationType.None,
            _ => AuthenticationType.None
        };
    }
}
