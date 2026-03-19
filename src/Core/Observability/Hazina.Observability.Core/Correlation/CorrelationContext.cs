using System;
using System.Threading;

namespace Hazina.Observability.Core.Correlation;

/// <summary>
/// Provides ambient correlation ID tracking that works both inside and outside HTTP requests.
/// Uses AsyncLocal to maintain correlation ID across async boundaries.
/// </summary>
public static class CorrelationContext
{
    private static readonly AsyncLocal<string?> _correlationId = new();

    /// <summary>
    /// Gets or sets the current correlation ID for this execution context
    /// </summary>
    public static string? CurrentCorrelationId
    {
        get => _correlationId.Value;
        set => _correlationId.Value = value;
    }

    /// <summary>
    /// Gets the current correlation ID or generates a new one if none exists
    /// </summary>
    public static string GetOrCreateCorrelationId()
    {
        if (string.IsNullOrEmpty(_correlationId.Value))
        {
            _correlationId.Value = GenerateCorrelationId();
        }
        return _correlationId.Value!;
    }

    /// <summary>
    /// Generates a new correlation ID
    /// </summary>
    public static string GenerateCorrelationId()
    {
        return Guid.NewGuid().ToString("N");
    }

    /// <summary>
    /// Creates a disposable scope for a correlation ID
    /// </summary>
    public static IDisposable BeginScope(string? correlationId = null)
    {
        return new CorrelationScope(correlationId ?? GenerateCorrelationId());
    }

    private sealed class CorrelationScope : IDisposable
    {
        private readonly string? _previousCorrelationId;

        public CorrelationScope(string correlationId)
        {
            _previousCorrelationId = _correlationId.Value;
            _correlationId.Value = correlationId;
        }

        public void Dispose()
        {
            _correlationId.Value = _previousCorrelationId;
        }
    }
}
