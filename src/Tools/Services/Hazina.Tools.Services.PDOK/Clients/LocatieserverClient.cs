using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using Hazina.Tools.Services.PDOK.Models;

namespace Hazina.Tools.Services.PDOK.Clients;

/// <summary>
/// Client for PDOK Locatieserver (Geocoding and reverse geocoding service)
/// Free geocoding service for Dutch addresses and locations
/// API: https://api.pdok.nl/bzk/locatieserver/search/v3_1/
/// </summary>
public class LocatieserverClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private const string DefaultBaseUrl = "https://api.pdok.nl/bzk/locatieserver/search/v3_1";

    public LocatieserverClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { BaseAddress = new Uri(DefaultBaseUrl) };

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    /// <summary>
    /// Geocode an address or location query
    /// Returns coordinates and detailed address information
    /// </summary>
    public async Task<List<LocatieserverDocument>> GeocodeAsync(
        string query,
        string? type = null,
        int maxResults = 10,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new List<string>
        {
            $"q={HttpUtility.UrlEncode(query)}",
            $"rows={maxResults}",
            "fl=*",
            "fq=*:*"
        };

        if (!string.IsNullOrEmpty(type))
        {
            queryParams.Add($"fq=type:{type}");
        }

        var url = $"/free?{string.Join("&", queryParams)}";
        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<LocatieserverResponse>(_jsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize geocoding response");

        // Parse coordinates from WKT format "POINT(lon lat)"
        foreach (var doc in result.Response?.Docs ?? new List<LocatieserverDocument>())
        {
            ParseCoordinates(doc);
        }

        return result.Response?.Docs ?? new List<LocatieserverDocument>();
    }

    /// <summary>
    /// Reverse geocode coordinates to find nearest address
    /// </summary>
    public async Task<List<LocatieserverDocument>> ReverseGeocodeAsync(
        double longitude,
        double latitude,
        string? type = null,
        int maxResults = 1,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new List<string>
        {
            $"lon={longitude:F6}",
            $"lat={latitude:F6}",
            $"rows={maxResults}",
            "fl=*",
            "fq=*:*"
        };

        if (!string.IsNullOrEmpty(type))
        {
            queryParams.Add($"fq=type:{type}");
        }

        var url = $"/reverse?{string.Join("&", queryParams)}";
        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<LocatieserverResponse>(_jsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize reverse geocoding response");

        // Parse coordinates
        foreach (var doc in result.Response?.Docs ?? new List<LocatieserverDocument>())
        {
            ParseCoordinates(doc);
        }

        return result.Response?.Docs ?? new List<LocatieserverDocument>();
    }

    /// <summary>
    /// Find addresses by postal code
    /// </summary>
    public async Task<List<LocatieserverDocument>> FindByPostalCodeAsync(
        string postalCode,
        int? houseNumber = null,
        int maxResults = 100,
        CancellationToken cancellationToken = default)
    {
        var query = postalCode.Replace(" ", "");
        if (houseNumber.HasValue)
        {
            query += $" {houseNumber}";
        }

        return await GeocodeAsync(query, LocatieserverTypes.Address, maxResults, cancellationToken);
    }

    /// <summary>
    /// Find addresses by street and city
    /// </summary>
    public async Task<List<LocatieserverDocument>> FindByStreetAndCityAsync(
        string street,
        string city,
        int? houseNumber = null,
        int maxResults = 100,
        CancellationToken cancellationToken = default)
    {
        var query = $"{street}, {city}";
        if (houseNumber.HasValue)
        {
            query = $"{street} {houseNumber}, {city}";
        }

        return await GeocodeAsync(query, LocatieserverTypes.Address, maxResults, cancellationToken);
    }

    /// <summary>
    /// Suggest addresses as user types (autocomplete)
    /// </summary>
    public async Task<List<LocatieserverDocument>> SuggestAsync(
        string partialQuery,
        int maxResults = 10,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new List<string>
        {
            $"q={HttpUtility.UrlEncode(partialQuery)}",
            $"rows={maxResults}",
            "fl=*",
            "fq=*:*"
        };

        var url = $"/suggest?{string.Join("&", queryParams)}";
        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<LocatieserverResponse>(_jsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize suggest response");

        foreach (var doc in result.Response?.Docs ?? new List<LocatieserverDocument>())
        {
            ParseCoordinates(doc);
        }

        return result.Response?.Docs ?? new List<LocatieserverDocument>();
    }

    /// <summary>
    /// Lookup specific object by ID
    /// </summary>
    public async Task<LocatieserverDocument?> LookupAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new List<string>
        {
            $"id={HttpUtility.UrlEncode(id)}",
            "fl=*"
        };

        var url = $"/lookup?{string.Join("&", queryParams)}";
        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<LocatieserverResponse>(_jsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize lookup response");

        var doc = result.Response?.Docs?.FirstOrDefault();
        if (doc != null)
        {
            ParseCoordinates(doc);
        }

        return doc;
    }

    /// <summary>
    /// Parse WKT POINT format to longitude and latitude
    /// Example: "POINT(5.12345 52.98765)" -> Lon=5.12345, Lat=52.98765
    /// </summary>
    private static void ParseCoordinates(LocatieserverDocument doc)
    {
        if (string.IsNullOrEmpty(doc.CentroideLl))
            return;

        var wkt = doc.CentroideLl.Replace("POINT(", "").Replace(")", "").Trim();
        var coords = wkt.Split(' ');

        if (coords.Length == 2 &&
            double.TryParse(coords[0], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var lon) &&
            double.TryParse(coords[1], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var lat))
        {
            doc.Longitude = lon;
            doc.Latitude = lat;
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        GC.SuppressFinalize(this);
    }
}
