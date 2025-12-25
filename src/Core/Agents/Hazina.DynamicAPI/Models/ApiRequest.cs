namespace DevGPT.DynamicAPI.Models;

public class ApiRequest
{
    public string Url { get; set; } = string.Empty;
    public string Method { get; set; } = "GET";
    public Dictionary<string, string>? Headers { get; set; }
    public object? Body { get; set; }
    public Dictionary<string, string>? QueryParameters { get; set; }
    public AuthenticationType AuthType { get; set; } = AuthenticationType.None;
    public string? ServiceName { get; set; }
}

public enum AuthenticationType
{
    None,
    Bearer,
    ApiKey,
    Basic,
    OAuth2
}

public class ApiResponse
{
    public int StatusCode { get; set; }
    public string? Body { get; set; }
    public Dictionary<string, string> Headers { get; set; } = new();
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ApiDocumentation
{
    public string ServiceName { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public List<ApiEndpoint> Endpoints { get; set; } = new();
    public AuthenticationType AuthType { get; set; }
    public Dictionary<string, string> RequiredCredentials { get; set; } = new();
}

public class ApiEndpoint
{
    public string Path { get; set; } = string.Empty;
    public string Method { get; set; } = "GET";
    public string Description { get; set; } = string.Empty;
    public List<ApiParameter> Parameters { get; set; } = new();
    public Dictionary<string, string> RequiredHeaders { get; set; } = new();
}

public class ApiParameter
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "string";
    public bool Required { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Location { get; set; } = "query"; // query, path, header, body
}
