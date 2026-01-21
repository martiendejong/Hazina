using Hazina.Tools.Services.PDOK.Models;

namespace Hazina.Tools.Services.PDOK.Clients;

/// <summary>
/// Client for BRK Kadastrale Kaart (Cadastral Map)
/// Provides access to cadastral boundaries, parcels, and related map information
/// API: https://api.pdok.nl/kadaster/brk-kadastrale-kaart/ogc/v1
/// </summary>
public class KadastraleKaartClient : OgcApiClient
{
    private const string DefaultBaseUrl = "https://api.pdok.nl/kadaster/brk-kadastrale-kaart/ogc/v1";

    public KadastraleKaartClient(HttpClient? httpClient = null)
        : base(DefaultBaseUrl, httpClient)
    {
    }

    public KadastraleKaartClient(string baseUrl, HttpClient? httpClient = null)
        : base(baseUrl, httpClient)
    {
    }

    #region Kadastrale Grenzen (Cadastral Boundaries)

    /// <summary>
    /// Find cadastral boundaries within a bounding box
    /// </summary>
    public async Task<FeatureCollection<KadastraleGrens>> FindKadastraleGrenzenAsync(
        double minLon,
        double minLat,
        double maxLon,
        double maxLat,
        int limit = 1000,
        CancellationToken cancellationToken = default)
    {
        var bbox = CreateBbox(minLon, minLat, maxLon, maxLat);
        return await QueryFeaturesAsync<KadastraleGrens>("kadastralegrenzen", limit, null, bbox, null, null, cancellationToken);
    }

    /// <summary>
    /// Find cadastral boundaries near a point
    /// </summary>
    public async Task<FeatureCollection<KadastraleGrens>> FindKadastraleGrenzenNearAsync(
        double lon,
        double lat,
        double radiusMeters = 500,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var bbox = CreateBboxAroundPoint(lon, lat, radiusMeters);
        return await QueryFeaturesAsync<KadastraleGrens>("kadastralegrenzen", limit, null, bbox, null, null, cancellationToken);
    }

    #endregion

    #region Perceel (Cadastral Parcels)

    /// <summary>
    /// Find cadastral parcels within a bounding box
    /// </summary>
    public async Task<FeatureCollection<Perceel>> FindPercelenAsync(
        double minLon,
        double minLat,
        double maxLon,
        double maxLat,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var bbox = CreateBbox(minLon, minLat, maxLon, maxLat);
        return await QueryFeaturesAsync<Perceel>("perceel", limit, null, bbox, null, null, cancellationToken);
    }

    /// <summary>
    /// Find cadastral parcel at a specific location
    /// </summary>
    public async Task<FeatureCollection<Perceel>> FindPerceelAtLocationAsync(
        double lon,
        double lat,
        CancellationToken cancellationToken = default)
    {
        // Use very small radius to find parcel at point
        var bbox = CreateBboxAroundPoint(lon, lat, 1);
        return await QueryFeaturesAsync<Perceel>("perceel", 1, null, bbox, null, null, cancellationToken);
    }

    /// <summary>
    /// Get a specific cadastral parcel by ID
    /// </summary>
    public async Task<Feature<Perceel>> GetPerceelAsync(
        string perceelId,
        CancellationToken cancellationToken = default)
    {
        return await GetFeatureAsync<Perceel>("perceel", perceelId, null, cancellationToken);
    }

    /// <summary>
    /// Find cadastral parcels by cadastral municipality and section
    /// </summary>
    public async Task<FeatureCollection<Perceel>> FindPercelenByGemeenteAndSectieAsync(
        string kadastraleGemeenteCode,
        string sectie,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var filters = new Dictionary<string, string>
        {
            ["kadastraleGemeenteCode"] = kadastraleGemeenteCode,
            ["sectie"] = sectie
        };
        return await QueryFeaturesAsync<Perceel>("perceel", limit, null, null, filters, null, cancellationToken);
    }

    #endregion

    #region Openbare Ruimte Naam (Public Space Names)

    /// <summary>
    /// Find public space names within a bounding box
    /// </summary>
    public async Task<FeatureCollection<OpenbareRuimteNaam>> FindOpenbareRuimteNamenAsync(
        double minLon,
        double minLat,
        double maxLon,
        double maxLat,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var bbox = CreateBbox(minLon, minLat, maxLon, maxLat);
        return await QueryFeaturesAsync<OpenbareRuimteNaam>("openbareruimtenaam", limit, null, bbox, null, null, cancellationToken);
    }

    #endregion

    #region Bebouwing (Buildings)

    /// <summary>
    /// Find buildings on cadastral map within a bounding box
    /// </summary>
    public async Task<FeatureCollection<Bebouwing>> FindBebouwingAsync(
        double minLon,
        double minLat,
        double maxLon,
        double maxLat,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var bbox = CreateBbox(minLon, minLat, maxLon, maxLat);
        return await QueryFeaturesAsync<Bebouwing>("bebouwing", limit, null, bbox, null, null, cancellationToken);
    }

    #endregion

    #region Nummeraanduidingreeks (House Number Ranges)

    /// <summary>
    /// Find house number ranges within a bounding box
    /// </summary>
    public async Task<FeatureCollection<Nummeraanduidingreeks>> FindNummeraanduidingreeksenAsync(
        double minLon,
        double minLat,
        double maxLon,
        double maxLat,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var bbox = CreateBbox(minLon, minLat, maxLon, maxLat);
        return await QueryFeaturesAsync<Nummeraanduidingreeks>("nummeraanduidingreeks", limit, null, bbox, null, null, cancellationToken);
    }

    #endregion
}
