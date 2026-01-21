using Hazina.Tools.Services.PDOK.Models;

namespace Hazina.Tools.Services.PDOK.Clients;

/// <summary>
/// Client for BRK Kadastrale Percelen (INSPIRE harmonized cadastral parcels)
/// Provides access to cadastral parcels in the international INSPIRE standard format
/// API: https://api.pdok.nl/kadaster/brk-kadastrale-percelen/ogc/v1
/// </summary>
public class KadastralePercelenClient : OgcApiClient
{
    private const string DefaultBaseUrl = "https://api.pdok.nl/kadaster/brk-kadastrale-percelen/ogc/v1";

    public KadastralePercelenClient(HttpClient? httpClient = null)
        : base(DefaultBaseUrl, httpClient)
    {
    }

    public KadastralePercelenClient(string baseUrl, HttpClient? httpClient = null)
        : base(baseUrl, httpClient)
    {
    }

    /// <summary>
    /// Find INSPIRE cadastral parcels within a bounding box
    /// </summary>
    public async Task<FeatureCollection<InspireCadastralParcel>> FindCadastralParcelsAsync(
        double minLon,
        double minLat,
        double maxLon,
        double maxLat,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var bbox = CreateBbox(minLon, minLat, maxLon, maxLat);
        return await QueryFeaturesAsync<InspireCadastralParcel>("cadastralparcel", limit, null, bbox, null, null, cancellationToken);
    }

    /// <summary>
    /// Find INSPIRE cadastral parcel at a specific location
    /// </summary>
    public async Task<FeatureCollection<InspireCadastralParcel>> FindCadastralParcelAtLocationAsync(
        double lon,
        double lat,
        CancellationToken cancellationToken = default)
    {
        var bbox = CreateBboxAroundPoint(lon, lat, 1);
        return await QueryFeaturesAsync<InspireCadastralParcel>("cadastralparcel", 1, null, bbox, null, null, cancellationToken);
    }

    /// <summary>
    /// Get a specific INSPIRE cadastral parcel by ID
    /// </summary>
    public async Task<Feature<InspireCadastralParcel>> GetCadastralParcelAsync(
        string parcelId,
        CancellationToken cancellationToken = default)
    {
        return await GetFeatureAsync<InspireCadastralParcel>("cadastralparcel", parcelId, null, cancellationToken);
    }

    /// <summary>
    /// Find cadastral parcels near a point
    /// </summary>
    public async Task<FeatureCollection<InspireCadastralParcel>> FindCadastralParcelsNearAsync(
        double lon,
        double lat,
        double radiusMeters = 500,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var bbox = CreateBboxAroundPoint(lon, lat, radiusMeters);
        return await QueryFeaturesAsync<InspireCadastralParcel>("cadastralparcel", limit, null, bbox, null, null, cancellationToken);
    }
}
