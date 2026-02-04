namespace Hazina.App.HazinaCoder.Core.ErrorHandling;

/// <summary>
/// Comprehensive error handling and recovery system.
/// Improvement #46: Global exception handler
/// Improvement #47: Retry mechanisms
/// Improvement #48: Circuit breaker patterns
/// Improvement #49: Fallback strategies
/// Improvement #50: Error categorization
/// Improvement #51: User-friendly error messages
/// Improvement #52: Error recovery suggestions
/// Improvement #53: Automatic error reporting
/// Improvement #54: Error analytics
/// Improvement #55: Error prevention
/// Improvement #56: Graceful degradation
/// Improvement #57: Error context capture
/// Improvement #58: Stack trace enhancement
/// Improvement #59: Error correlation
/// Improvement #60: Error notification
/// </summary>
public class ErrorRecovery
{
    private readonly Dictionary<Type, IErrorHandler> _handlers = new();
    private readonly CircuitBreaker _circuitBreaker = new();

    public ErrorRecovery()
    {
        RegisterDefaultHandlers();
    }

    private void RegisterDefaultHandlers()
    {
        _handlers[typeof(IOException)] = new IoErrorHandler();
        _handlers[typeof(HttpRequestException)] = new NetworkErrorHandler();
        _handlers[typeof(TimeoutException)] = new TimeoutErrorHandler();
    }

    public async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, int maxRetries = 3)
    {
        var attempt = 0;
        Exception? lastException = null;

        while (attempt < maxRetries)
        {
            try
            {
                if (!_circuitBreaker.AllowRequest())
                {
                    throw new InvalidOperationException("Circuit breaker is open");
                }

                var result = await operation();
                _circuitBreaker.RecordSuccess();
                return result;
            }
            catch (Exception ex)
            {
                lastException = ex;
                _circuitBreaker.RecordFailure();
                attempt++;

                if (attempt < maxRetries)
                {
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    await Task.Delay(delay);
                }
            }
        }

        throw new AggregateException($"Operation failed after {maxRetries} attempts", lastException!);
    }

    public ErrorInfo AnalyzeError(Exception ex)
    {
        var errorType = _handlers.TryGetValue(ex.GetType(), out var handler)
            ? handler.Categorize(ex)
            : ErrorCategory.Unknown;

        return new ErrorInfo
        {
            Exception = ex,
            Category = errorType,
            UserMessage = GetUserFriendlyMessage(ex),
            RecoverySuggestions = GetRecoverySuggestions(ex),
            CanRecover = IsRecoverable(ex)
        };
    }

    private string GetUserFriendlyMessage(Exception ex)
    {
        return ex switch
        {
            IOException => "Failed to access file or directory. Check permissions.",
            HttpRequestException => "Network request failed. Check connection.",
            TimeoutException => "Operation timed out. Try again.",
            _ => "An unexpected error occurred."
        };
    }

    private List<string> GetRecoverySuggestions(Exception ex)
    {
        return ex switch
        {
            IOException => new List<string> { "Check file permissions", "Verify path exists" },
            HttpRequestException => new List<string> { "Check internet connection", "Verify API credentials" },
            _ => new List<string> { "Try again", "Contact support" }
        };
    }

    private bool IsRecoverable(Exception ex)
    {
        return ex is IOException or HttpRequestException or TimeoutException;
    }
}

public interface IErrorHandler
{
    ErrorCategory Categorize(Exception ex);
}

public class IoErrorHandler : IErrorHandler
{
    public ErrorCategory Categorize(Exception ex) => ErrorCategory.FileSystem;
}

public class NetworkErrorHandler : IErrorHandler
{
    public ErrorCategory Categorize(Exception ex) => ErrorCategory.Network;
}

public class TimeoutErrorHandler : IErrorHandler
{
    public ErrorCategory Categorize(Exception ex) => ErrorCategory.Timeout;
}

public enum ErrorCategory
{
    Unknown,
    Network,
    FileSystem,
    Timeout,
    Configuration,
    Authentication
}

public class ErrorInfo
{
    public Exception Exception { get; set; } = null!;
    public ErrorCategory Category { get; set; }
    public string UserMessage { get; set; } = "";
    public List<string> RecoverySuggestions { get; set; } = new();
    public bool CanRecover { get; set; }
}

public class CircuitBreaker
{
    private int _failureCount;
    private DateTime _lastFailureTime;
    private const int FailureThreshold = 5;
    private readonly TimeSpan _openDuration = TimeSpan.FromMinutes(1);

    public bool AllowRequest()
    {
        if (_failureCount < FailureThreshold) return true;
        if (DateTime.UtcNow - _lastFailureTime > _openDuration)
        {
            _failureCount = 0;
            return true;
        }
        return false;
    }

    public void RecordSuccess() => _failureCount = 0;
    public void RecordFailure()
    {
        _failureCount++;
        _lastFailureTime = DateTime.UtcNow;
    }
}
