namespace Hazina.Tools.Services.Images.Abstractions;

/// <summary>
/// Options for configuring image provider behavior.
/// </summary>
public sealed class ImageProviderOptions
{
    /// <summary>
    /// The preferred provider to use for operations.
    /// If null, the system will automatically select the most appropriate provider.
    /// </summary>
    public string? PreferredProvider { get; init; }

    /// <summary>
    /// API key for AI-based providers (if required).
    /// </summary>
    public string? ApiKey { get; init; }

    /// <summary>
    /// Base URL for AI-based providers (if required).
    /// </summary>
    public string? BaseUrl { get; init; }

    /// <summary>
    /// Model to use for AI-based operations.
    /// </summary>
    public string? Model { get; init; }

    /// <summary>
    /// Quality setting for output images (0-100).
    /// </summary>
    public int Quality { get; init; } = 95;
}
