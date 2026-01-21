using Hazina.Tools.Services.PDOK.Models;

namespace Hazina.Tools.Services.PDOK.Clients;

/// <summary>
/// Client for BAG (Basisregistratie Adressen en Gebouwen) - Dutch Address and Building Registry
/// Provides access to addresses, buildings, and related data
/// API: https://api.pdok.nl/kadaster/bag/ogc/v2/
/// </summary>
public class BagClient : OgcApiClient
{
    private const string DefaultBaseUrl = "https://api.pdok.nl/kadaster/bag/ogc/v2";

    public BagClient(HttpClient? httpClient = null)
        : base(DefaultBaseUrl, httpClient)
    {
    }

    public BagClient(string baseUrl, HttpClient? httpClient = null)
        : base(baseUrl, httpClient)
    {
    }

    #region Verblijfsobject (Residential Objects / Addresses)

    /// <summary>
    /// Find residential objects (verblijfsobjecten) within a bounding box
    /// </summary>
    public async Task<FeatureCollection<Verblijfsobject>> FindVerblijfsobjectenAsync(
        double minLon,
        double minLat,
        double maxLon,
        double maxLat,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var bbox = CreateBbox(minLon, minLat, maxLon, maxLat);
        return await QueryFeaturesAsync<Verblijfsobject>("verblijfsobject", limit, null, bbox, null, null, cancellationToken);
    }

    /// <summary>
    /// Find residential objects near a point
    /// </summary>
    public async Task<FeatureCollection<Verblijfsobject>> FindVerblijfsobjectenNearAsync(
        double lon,
        double lat,
        double radiusMeters = 100,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var bbox = CreateBboxAroundPoint(lon, lat, radiusMeters);
        return await QueryFeaturesAsync<Verblijfsobject>("verblijfsobject", limit, null, bbox, null, null, cancellationToken);
    }

    /// <summary>
    /// Get a specific residential object by BAG ID
    /// </summary>
    public async Task<Feature<Verblijfsobject>> GetVerblijfsobjectAsync(
        string bagId,
        CancellationToken cancellationToken = default)
    {
        return await GetFeatureAsync<Verblijfsobject>("verblijfsobject", bagId, null, cancellationToken);
    }

    #endregion

    #region Pand (Buildings)

    /// <summary>
    /// Find buildings within a bounding box
    /// </summary>
    public async Task<FeatureCollection<Pand>> FindPandenAsync(
        double minLon,
        double minLat,
        double maxLon,
        double maxLat,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var bbox = CreateBbox(minLon, minLat, maxLon, maxLat);
        return await QueryFeaturesAsync<Pand>("pand", limit, null, bbox, null, null, cancellationToken);
    }

    /// <summary>
    /// Find buildings near a point
    /// </summary>
    public async Task<FeatureCollection<Pand>> FindPandenNearAsync(
        double lon,
        double lat,
        double radiusMeters = 100,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var bbox = CreateBboxAroundPoint(lon, lat, radiusMeters);
        return await QueryFeaturesAsync<Pand>("pand", limit, null, bbox, null, null, cancellationToken);
    }

    /// <summary>
    /// Get a specific building by BAG ID
    /// </summary>
    public async Task<Feature<Pand>> GetPandAsync(
        string bagId,
        CancellationToken cancellationToken = default)
    {
        return await GetFeatureAsync<Pand>("pand", bagId, null, cancellationToken);
    }

    #endregion

    #region Nummeraanduiding (Address Designations)

    /// <summary>
    /// Find address designations within a bounding box
    /// </summary>
    public async Task<FeatureCollection<Nummeraanduiding>> FindNummeraanduidingenAsync(
        double minLon,
        double minLat,
        double maxLon,
        double maxLat,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var bbox = CreateBbox(minLon, minLat, maxLon, maxLat);
        return await QueryFeaturesAsync<Nummeraanduiding>("nummeraanduiding", limit, null, bbox, null, null, cancellationToken);
    }

    /// <summary>
    /// Get a specific address designation by BAG ID
    /// </summary>
    public async Task<Feature<Nummeraanduiding>> GetNummeraanduidingAsync(
        string bagId,
        CancellationToken cancellationToken = default)
    {
        return await GetFeatureAsync<Nummeraanduiding>("nummeraanduiding", bagId, null, cancellationToken);
    }

    /// <summary>
    /// Find address designations by postal code
    /// </summary>
    public async Task<FeatureCollection<Nummeraanduiding>> FindNummeraanduidingenByPostcodeAsync(
        string postcode,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var filters = new Dictionary<string, string>
        {
            ["postcode"] = postcode.Replace(" ", "").ToUpper()
        };
        return await QueryFeaturesAsync<Nummeraanduiding>("nummeraanduiding", limit, null, null, filters, null, cancellationToken);
    }

    #endregion

    #region Openbare Ruimte (Public Spaces / Streets)

    /// <summary>
    /// Find public spaces (streets) by name
    /// </summary>
    public async Task<FeatureCollection<OpenbareRuimte>> FindOpenbareRuimtenAsync(
        string naam,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var filters = new Dictionary<string, string>
        {
            ["naam"] = naam
        };
        return await QueryFeaturesAsync<OpenbareRuimte>("openbareruimte", limit, null, null, filters, null, cancellationToken);
    }

    /// <summary>
    /// Get a specific public space by BAG ID
    /// </summary>
    public async Task<Feature<OpenbareRuimte>> GetOpenbareRuimteAsync(
        string bagId,
        CancellationToken cancellationToken = default)
    {
        return await GetFeatureAsync<OpenbareRuimte>("openbareruimte", bagId, null, cancellationToken);
    }

    #endregion

    #region Woonplaats (Cities/Towns)

    /// <summary>
    /// Find cities/towns by name
    /// </summary>
    public async Task<FeatureCollection<Woonplaats>> FindWoonplaatsenAsync(
        string naam,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var filters = new Dictionary<string, string>
        {
            ["naam"] = naam
        };
        return await QueryFeaturesAsync<Woonplaats>("woonplaats", limit, null, null, filters, null, cancellationToken);
    }

    /// <summary>
    /// Get a specific city/town by BAG ID
    /// </summary>
    public async Task<Feature<Woonplaats>> GetWoonplaatsAsync(
        string bagId,
        CancellationToken cancellationToken = default)
    {
        return await GetFeatureAsync<Woonplaats>("woonplaats", bagId, null, cancellationToken);
    }

    #endregion
}
