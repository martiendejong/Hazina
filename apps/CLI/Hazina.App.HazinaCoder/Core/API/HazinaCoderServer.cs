using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hazina.App.HazinaCoder.Core.API;

/// <summary>
/// HTTP server for HazinaCoder API
/// Exposes programmatic API over HTTP for remote access
/// </summary>
public class HazinaCoderServer : IDisposable
{
    private readonly HazinaCoderAPI _api;
    private readonly HttpListener _listener;
    private readonly int _port;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _serverTask;

    public bool IsRunning { get; private set; }

    public HazinaCoderServer(int port = 27185, HazinaCoderConfig? config = null)
    {
        _port = port;
        _api = new HazinaCoderAPI(config);
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/");
    }

    /// <summary>
    /// Start the HTTP server
    /// </summary>
    public async Task StartAsync()
    {
        if (IsRunning)
            return;

        _listener.Start();
        _cancellationTokenSource = new CancellationTokenSource();
        IsRunning = true;

        Console.WriteLine($"HazinaCoder API Server started on http://localhost:{_port}");
        Console.WriteLine("Available endpoints:");
        Console.WriteLine("  POST   /api/sessions           - Create new session");
        Console.WriteLine("  GET    /api/sessions           - List all sessions");
        Console.WriteLine("  GET    /api/sessions/{id}      - Get session info");
        Console.WriteLine("  DELETE /api/sessions/{id}      - End session");
        Console.WriteLine("  POST   /api/sessions/{id}/messages - Send message");
        Console.WriteLine("  POST   /api/sessions/{id}/images   - Analyze image");
        Console.WriteLine("  GET    /api/health             - Check provider health");

        _serverTask = Task.Run(async () => await ProcessRequestsAsync(_cancellationTokenSource.Token));

        await Task.CompletedTask;
    }

    /// <summary>
    /// Stop the HTTP server
    /// </summary>
    public async Task StopAsync()
    {
        if (!IsRunning)
            return;

        _cancellationTokenSource?.Cancel();
        _listener.Stop();
        IsRunning = false;

        if (_serverTask != null)
        {
            await _serverTask;
        }

        Console.WriteLine("HazinaCoder API Server stopped");
    }

    /// <summary>
    /// Process incoming HTTP requests
    /// </summary>
    private async Task ProcessRequestsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(async () => await HandleRequestAsync(context), cancellationToken);
            }
            catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing request: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Handle individual HTTP request
    /// </summary>
    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            var path = request.Url?.AbsolutePath ?? "/";
            var method = request.HttpMethod;

            Console.WriteLine($"{method} {path}");

            // Route request
            var result = (path, method) switch
            {
                ("/api/sessions", "POST") => await HandleCreateSessionAsync(request),
                ("/api/sessions", "GET") => await HandleListSessionsAsync(),
                _ when path.StartsWith("/api/sessions/") && method == "GET" =>
                    await HandleGetSessionAsync(path),
                _ when path.StartsWith("/api/sessions/") && path.EndsWith("/messages") && method == "POST" =>
                    await HandleSendMessageAsync(request, path),
                _ when path.StartsWith("/api/sessions/") && path.EndsWith("/images") && method == "POST" =>
                    await HandleAnalyzeImageAsync(request, path),
                _ when path.StartsWith("/api/sessions/") && method == "DELETE" =>
                    await HandleEndSessionAsync(path),
                ("/api/health", "GET") => await HandleHealthCheckAsync(),
                ("/", "GET") => HandleRoot(),
                _ => new ApiResponse { Success = false, Error = "Endpoint not found" }
            };

            // Send response
            var jsonResult = JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            var buffer = Encoding.UTF8.GetBytes(jsonResult);
            response.ContentType = "application/json";
            response.ContentLength64 = buffer.Length;
            response.StatusCode = result.Success ? 200 : (result.Error?.Contains("not found") == true ? 404 : 500);

            await response.OutputStream.WriteAsync(buffer);
            response.OutputStream.Close();
        }
        catch (Exception ex)
        {
            var errorResult = new ApiResponse
            {
                Success = false,
                Error = ex.Message
            };

            var jsonError = JsonSerializer.Serialize(errorResult);
            var buffer = Encoding.UTF8.GetBytes(jsonError);

            response.ContentType = "application/json";
            response.StatusCode = 500;
            response.ContentLength64 = buffer.Length;

            await response.OutputStream.WriteAsync(buffer);
            response.OutputStream.Close();
        }
    }

    private ApiResponse HandleRoot()
    {
        return new ApiResponse
        {
            Success = true,
            Data = new
            {
                Name = "HazinaCoder API",
                Version = "1.0.0",
                Endpoints = new[]
                {
                    "POST /api/sessions",
                    "GET /api/sessions",
                    "GET /api/sessions/{id}",
                    "DELETE /api/sessions/{id}",
                    "POST /api/sessions/{id}/messages",
                    "POST /api/sessions/{id}/images",
                    "GET /api/health"
                }
            }
        };
    }

    private async Task<ApiResponse> HandleCreateSessionAsync(HttpListenerRequest request)
    {
        var session = _api.CreateSession();

        return new ApiResponse
        {
            Success = true,
            Data = new
            {
                SessionId = session.SessionId,
                Created = DateTime.UtcNow
            }
        };
    }

    private async Task<ApiResponse> HandleListSessionsAsync()
    {
        var sessions = _api.ListSessions();

        return new ApiResponse
        {
            Success = true,
            Data = new
            {
                Sessions = sessions,
                Count = sessions.Count
            }
        };
    }

    private async Task<ApiResponse> HandleGetSessionAsync(string path)
    {
        var sessionId = ExtractSessionId(path);
        var session = _api.GetSession(sessionId);

        if (session == null)
        {
            return new ApiResponse
            {
                Success = false,
                Error = $"Session not found: {sessionId}"
            };
        }

        var state = session.GetState();

        return new ApiResponse
        {
            Success = true,
            Data = new
            {
                SessionId = session.SessionId,
                State = new
                {
                    state.IterationCount,
                    state.LastActivity,
                    MessageCount = state.ConversationHistory.Count,
                    state.WorkingDirectory
                }
            }
        };
    }

    private async Task<ApiResponse> HandleSendMessageAsync(HttpListenerRequest request, string path)
    {
        var sessionId = ExtractSessionId(path);
        var session = _api.GetSession(sessionId);

        if (session == null)
        {
            return new ApiResponse
            {
                Success = false,
                Error = $"Session not found: {sessionId}"
            };
        }

        using var reader = new StreamReader(request.InputStream);
        var body = await reader.ReadToEndAsync();
        var messageRequest = JsonSerializer.Deserialize<MessageRequest>(body);

        if (messageRequest == null || string.IsNullOrEmpty(messageRequest.Message))
        {
            return new ApiResponse
            {
                Success = false,
                Error = "Message is required"
            };
        }

        var response = await session.SendMessageAsync(messageRequest.Message);

        return new ApiResponse
        {
            Success = true,
            Data = new
            {
                Response = response,
                SessionId = session.SessionId
            }
        };
    }

    private async Task<ApiResponse> HandleAnalyzeImageAsync(HttpListenerRequest request, string path)
    {
        var sessionId = ExtractSessionId(path);
        var session = _api.GetSession(sessionId);

        if (session == null)
        {
            return new ApiResponse
            {
                Success = false,
                Error = $"Session not found: {sessionId}"
            };
        }

        using var reader = new StreamReader(request.InputStream);
        var body = await reader.ReadToEndAsync();
        var imageRequest = JsonSerializer.Deserialize<ImageRequest>(body);

        if (imageRequest == null || string.IsNullOrEmpty(imageRequest.ImagePath))
        {
            return new ApiResponse
            {
                Success = false,
                Error = "ImagePath is required"
            };
        }

        var analysis = await session.AnalyzeImageAsync(
            imageRequest.ImagePath,
            imageRequest.Query ?? "Analyze this image");

        return new ApiResponse
        {
            Success = true,
            Data = new
            {
                Analysis = analysis,
                ImagePath = imageRequest.ImagePath
            }
        };
    }

    private async Task<ApiResponse> HandleEndSessionAsync(string path)
    {
        var sessionId = ExtractSessionId(path);

        await _api.EndSessionAsync(sessionId);

        return new ApiResponse
        {
            Success = true,
            Data = new
            {
                SessionId = sessionId,
                Message = "Session ended"
            }
        };
    }

    private async Task<ApiResponse> HandleHealthCheckAsync()
    {
        var health = await _api.CheckProvidersHealthAsync();

        return new ApiResponse
        {
            Success = true,
            Data = new
            {
                Providers = health,
                Timestamp = DateTime.UtcNow
            }
        };
    }

    private string ExtractSessionId(string path)
    {
        // Extract session ID from path like /api/sessions/{id} or /api/sessions/{id}/messages
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 2 ? parts[2] : string.Empty;
    }

    public void Dispose()
    {
        StopAsync().Wait();
        _listener.Close();
        _api.Dispose();
        _cancellationTokenSource?.Dispose();
    }
}

/// <summary>
/// API response wrapper
/// </summary>
public class ApiResponse
{
    public bool Success { get; set; }
    public object? Data { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Message request
/// </summary>
public class MessageRequest
{
    public string Message { get; set; } = string.Empty;
    public string? Model { get; set; }
    public int? MaxTokens { get; set; }
}

/// <summary>
/// Image analysis request
/// </summary>
public class ImageRequest
{
    public string ImagePath { get; set; } = string.Empty;
    public string? Query { get; set; }
}
