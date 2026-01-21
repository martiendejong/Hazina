using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hazina.Tools.Services.PDOK.Models;
using NetTopologySuite.Geometries;

namespace Hazina.Tools.Services.PDOK.Clients;

/// <summary>
/// Base client for OGC API Features services
/// Implements common functionality for querying collections, features, and spatial filtering
/// </summary>
public abstract class OgcApiClient : IDisposable
{
    protected readonly HttpClient _httpClient;
    protected readonly JsonSerializerOptions _jsonOptions;
    protected readonly string _baseUrl;

    protected OgcApiClient(string baseUrl, HttpClient? httpClient = null)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _httpClient = httpClient ?? new HttpClient { BaseAddress = new Uri(_baseUrl) };

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    /// <summary>
    /// Get all available collections
    /// </summary>
    public async Task<Collections> GetCollectionsAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync("/collections?f=json", cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Collections>(_jsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize collections");
    }

    /// <summary>
    /// Get metadata for a specific collection
    /// </summary>
    public async Task<Collection> GetCollectionAsync(string collectionId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"/collections/{collectionId}?f=json", cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Collection>(_jsonOptions, cancellationToken)
            ?? throw new InvalidOperationException($"Failed to deserialize collection {collectionId}");
    }

    /// <summary>
    /// Query features from a collection
    /// </summary>
    protected async Task<FeatureCollection<T>> QueryFeaturesAsync<T>(
        string collectionId,
        int? limit = null,
        int? offset = null,
        List<double>? bbox = null,
        Dictionary<string, string>? filters = null,
        string? crs = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var queryParams = new List<string> { "f=json" };

        if (limit.HasValue)
            queryParams.Add($"limit={limit.Value}");

        if (offset.HasValue)
            queryParams.Add($"offset={offset.Value}");

        if (bbox != null && bbox.Count == 4)
            queryParams.Add($"bbox={string.Join(",", bbox)}");

        if (crs != null)
            queryParams.Add($"crs={Uri.EscapeDataString(crs)}");

        if (filters != null)
        {
            foreach (var (key, value) in filters)
            {
                queryParams.Add($"{key}={Uri.EscapeDataString(value)}");
            }
        }

        var url = $"/collections/{collectionId}/items?{string.Join("&", queryParams)}";
        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<FeatureCollection<T>>(_jsonOptions, cancellationToken)
            ?? throw new InvalidOperationException($"Failed to deserialize features from {collectionId}");
    }

    /// <summary>
    /// Get a single feature by ID
    /// </summary>
    protected async Task<Feature<T>> GetFeatureAsync<T>(
        string collectionId,
        string featureId,
        string? crs = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var queryParams = new List<string> { "f=json" };

        if (crs != null)
            queryParams.Add($"crs={Uri.EscapeDataString(crs)}");

        var url = $"/collections/{collectionId}/items/{featureId}?{string.Join("&", queryParams)}";
        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Feature<T>>(_jsonOptions, cancellationToken)
            ?? throw new InvalidOperationException($"Failed to deserialize feature {featureId}");
    }

    /// <summary>
    /// Create bounding box from coordinates (minLon, minLat, maxLon, maxLat)
    /// </summary>
    protected static List<double> CreateBbox(double minLon, double minLat, double maxLon, double maxLat)
    {
        return new List<double> { minLon, minLat, maxLon, maxLat };
    }

    /// <summary>
    /// Create bounding box around a point with radius in meters (approximate)
    /// Uses rough approximation: 1 degree ≈ 111km
    /// </summary>
    protected static List<double> CreateBboxAroundPoint(double lon, double lat, double radiusMeters)
    {
        var radiusDegrees = radiusMeters / 111000.0; // Rough approximation
        return CreateBbox(
            lon - radiusDegrees,
            lat - radiusDegrees,
            lon + radiusDegrees,
            lat + radiusDegrees
        );
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        GC.SuppressFinalize(this);
    }
}
