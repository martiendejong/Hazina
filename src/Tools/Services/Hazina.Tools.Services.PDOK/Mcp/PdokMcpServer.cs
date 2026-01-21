using System.Text.Json;
using System.Text.Json.Serialization;
using Hazina.Tools.Services.PDOK.Clients;
using Hazina.Tools.Services.PDOK.Models;

namespace Hazina.Tools.Services.PDOK.Mcp;

/// <summary>
/// MCP (Model Context Protocol) Server for PDOK services
/// Exposes Dutch Kadaster open data as tools for AI agents
///
/// Available tools:
/// - pdok_geocode: Find coordinates for Dutch address
/// - pdok_reverse_geocode: Find address for coordinates
/// - pdok_find_address: Search addresses by postal code or street+city
/// - pdok_find_building: Get building information
/// - pdok_find_cadastral_parcel: Find cadastral parcel by location
/// - pdok_get_cadastral_info: Get detailed cadastral information
/// </summary>
public class PdokMcpServer : IDisposable
{
    private readonly BagClient _bagClient;
    private readonly KadastraleKaartClient _kadastraleKaartClient;
    private readonly KadastralePercelenClient _kadastralePercelenClient;
    private readonly LocatieserverClient _locatieserverClient;
    private readonly JsonSerializerOptions _jsonOptions;

    public PdokMcpServer(
        BagClient? bagClient = null,
        KadastraleKaartClient? kadastraleKaartClient = null,
        KadastralePercelenClient? kadastralePercelenClient = null,
        LocatieserverClient? locatieserverClient = null)
    {
        _bagClient = bagClient ?? new BagClient();
        _kadastraleKaartClient = kadastraleKaartClient ?? new KadastraleKaartClient();
        _kadastralePercelenClient = kadastralePercelenClient ?? new KadastralePercelenClient();
        _locatieserverClient = locatieserverClient ?? new LocatieserverClient();

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = true
        };
    }

    #region Geocoding Tools

    /// <summary>
    /// pdok_geocode: Find coordinates for a Dutch address
    /// </summary>
    /// <param name="query">Address query (e.g., "Nieuwe Gracht 1, Utrecht" or "1012AA 15")</param>
    /// <param name="maxResults">Maximum number of results (default: 10)</param>
    public async Task<string> GeocodeAsync(string query, int maxResults = 10)
    {
        try
        {
            var results = await _locatieserverClient.GeocodeAsync(query, null, maxResults);

            var simplified = results.Select(r => new
            {
                address = r.Weergavenaam,
                longitude = r.Longitude,
                latitude = r.Latitude,
                city = r.Woonplaatsnaam,
                municipality = r.Gemeentenaam,
                province = r.Provincienaam,
                postalCode = r.Postcode,
                type = r.Type,
                score = r.Score,
                bagId = r.NummeraanduidingId
            }).ToList();

            return JsonSerializer.Serialize(new { success = true, results = simplified }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    /// <summary>
    /// pdok_reverse_geocode: Find nearest address for coordinates
    /// </summary>
    /// <param name="longitude">Longitude (WGS84)</param>
    /// <param name="latitude">Latitude (WGS84)</param>
    /// <param name="maxResults">Maximum number of results (default: 1)</param>
    public async Task<string> ReverseGeocodeAsync(double longitude, double latitude, int maxResults = 1)
    {
        try
        {
            var results = await _locatieserverClient.ReverseGeocodeAsync(longitude, latitude, null, maxResults);

            if (results.Count == 0)
            {
                return JsonSerializer.Serialize(new { success = true, result = (object?)null, message = "No address found at this location" }, _jsonOptions);
            }

            var result = results.First();
            var simplified = new
            {
                address = result.Weergavenaam,
                longitude = result.Longitude,
                latitude = result.Latitude,
                city = result.Woonplaatsnaam,
                municipality = result.Gemeentenaam,
                province = result.Provincienaam,
                postalCode = result.Postcode,
                type = result.Type,
                bagId = result.NummeraanduidingId
            };

            return JsonSerializer.Serialize(new { success = true, result = simplified }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    #endregion

    #region BAG (Address & Building) Tools

    /// <summary>
    /// pdok_find_address: Search addresses by postal code or street and city
    /// </summary>
    /// <param name="postalCode">Dutch postal code (e.g., "1012AA")</param>
    /// <param name="houseNumber">Optional house number</param>
    /// <param name="street">Optional street name (if not using postal code)</param>
    /// <param name="city">Optional city name (if not using postal code)</param>
    /// <param name="maxResults">Maximum number of results (default: 100)</param>
    public async Task<string> FindAddressAsync(
        string? postalCode = null,
        int? houseNumber = null,
        string? street = null,
        string? city = null,
        int maxResults = 100)
    {
        try
        {
            List<LocatieserverDocument> results;

            if (!string.IsNullOrEmpty(postalCode))
            {
                results = await _locatieserverClient.FindByPostalCodeAsync(postalCode, houseNumber, maxResults);
            }
            else if (!string.IsNullOrEmpty(street) && !string.IsNullOrEmpty(city))
            {
                results = await _locatieserverClient.FindByStreetAndCityAsync(street, city, houseNumber, maxResults);
            }
            else
            {
                return JsonSerializer.Serialize(new { success = false, error = "Either postalCode or both street and city must be provided" }, _jsonOptions);
            }

            var simplified = results.Select(r => new
            {
                address = r.Weergavenaam,
                street = r.Openbareruimtenaam,
                houseNumber = r.HuisNlt,
                postalCode = r.Postcode,
                city = r.Woonplaatsnaam,
                municipality = r.Gemeentenaam,
                longitude = r.Longitude,
                latitude = r.Latitude,
                bagId = r.NummeraanduidingId
            }).ToList();

            return JsonSerializer.Serialize(new { success = true, count = simplified.Count, results = simplified }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    /// <summary>
    /// pdok_find_building: Get building information at a location
    /// </summary>
    /// <param name="longitude">Longitude (WGS84)</param>
    /// <param name="latitude">Latitude (WGS84)</param>
    /// <param name="radiusMeters">Search radius in meters (default: 100)</param>
    /// <param name="maxResults">Maximum number of results (default: 10)</param>
    public async Task<string> FindBuildingAsync(double longitude, double latitude, double radiusMeters = 100, int maxResults = 10)
    {
        try
        {
            var results = await _bagClient.FindPandenNearAsync(longitude, latitude, radiusMeters, maxResults);

            var simplified = results.Features.Select(f => new
            {
                bagId = f.Properties?.Identificatie,
                status = f.Properties?.Status,
                buildYear = f.Properties?.OorspronkelijkBouwjaar,
                geometry = f.Geometry?.AsText(),
                validFrom = f.Properties?.BeginGeldigheid,
                validTo = f.Properties?.EindGeldigheid
            }).ToList();

            return JsonSerializer.Serialize(new { success = true, count = simplified.Count, results = simplified }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    #endregion

    #region Cadastral (Kadaster) Tools

    /// <summary>
    /// pdok_find_cadastral_parcel: Find cadastral parcel at a location
    /// </summary>
    /// <param name="longitude">Longitude (WGS84)</param>
    /// <param name="latitude">Latitude (WGS84)</param>
    public async Task<string> FindCadastralParcelAsync(double longitude, double latitude)
    {
        try
        {
            var results = await _kadastraleKaartClient.FindPerceelAtLocationAsync(longitude, latitude);

            if (results.Features.Count == 0)
            {
                return JsonSerializer.Serialize(new { success = true, result = (object?)null, message = "No cadastral parcel found at this location" }, _jsonOptions);
            }

            var parcel = results.Features.First();
            var simplified = new
            {
                parcelId = parcel.Properties?.Identificatie,
                parcelNumber = parcel.Properties?.Perceelnummer,
                akrDesignation = parcel.Properties?.AkrAanduiding,
                municipality = parcel.Properties?.KadastraleGemeente,
                municipalityCode = parcel.Properties?.KadastraleGemeenteCode,
                section = parcel.Properties?.Sectie,
                area = parcel.Properties?.Oppervlakte,
                geometry = parcel.Geometry?.AsText(),
                validFrom = parcel.Properties?.BeginGeldigheid,
                validTo = parcel.Properties?.EindGeldigheid
            };

            return JsonSerializer.Serialize(new { success = true, result = simplified }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    /// <summary>
    /// pdok_get_cadastral_info: Get detailed cadastral information for an area
    /// </summary>
    /// <param name="longitude">Longitude (WGS84)</param>
    /// <param name="latitude">Latitude (WGS84)</param>
    /// <param name="radiusMeters">Search radius in meters (default: 500)</param>
    /// <param name="maxParcels">Maximum number of parcels to return (default: 10)</param>
    public async Task<string> GetCadastralInfoAsync(double longitude, double latitude, double radiusMeters = 500, int maxParcels = 10)
    {
        try
        {
            // Get cadastral parcels
            var parcels = await _kadastraleKaartClient.FindPercelenAsync(
                longitude - radiusMeters / 111000,
                latitude - radiusMeters / 111000,
                longitude + radiusMeters / 111000,
                latitude + radiusMeters / 111000,
                maxParcels);

            // Get cadastral boundaries
            var boundaries = await _kadastraleKaartClient.FindKadastraleGrenzenNearAsync(longitude, latitude, radiusMeters, 100);

            var result = new
            {
                parcels = parcels.Features.Select(f => new
                {
                    parcelId = f.Properties?.Identificatie,
                    parcelNumber = f.Properties?.Perceelnummer,
                    municipality = f.Properties?.KadastraleGemeente,
                    section = f.Properties?.Sectie,
                    area = f.Properties?.Oppervlakte
                }).ToList(),
                boundariesCount = boundaries.Features.Count,
                searchLocation = new { longitude, latitude },
                radiusMeters
            };

            return JsonSerializer.Serialize(new { success = true, result }, _jsonOptions);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, _jsonOptions);
        }
    }

    #endregion

    #region Tool Definitions for MCP Protocol

    /// <summary>
    /// Get MCP tool definitions for all PDOK tools
    /// Use this to register tools with an MCP server
    /// </summary>
    public static List<McpToolDefinition> GetToolDefinitions()
    {
        return new List<McpToolDefinition>
        {
            new McpToolDefinition
            {
                Name = "pdok_geocode",
                Description = "Find coordinates for a Dutch address. Supports full addresses, postal codes, street names, or city names.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        query = new
                        {
                            type = "string",
                            description = "Address query (e.g., 'Nieuwe Gracht 1, Utrecht' or '1012AA 15')"
                        },
                        maxResults = new
                        {
                            type = "integer",
                            description = "Maximum number of results to return (default: 10)",
                            @default = 10,
                            minimum = 1,
                            maximum = 100
                        }
                    },
                    required = new[] { "query" }
                }
            },
            new McpToolDefinition
            {
                Name = "pdok_reverse_geocode",
                Description = "Find the nearest Dutch address for given coordinates (longitude, latitude in WGS84).",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        longitude = new
                        {
                            type = "number",
                            description = "Longitude in WGS84 (decimal degrees)"
                        },
                        latitude = new
                        {
                            type = "number",
                            description = "Latitude in WGS84 (decimal degrees)"
                        },
                        maxResults = new
                        {
                            type = "integer",
                            description = "Maximum number of results (default: 1)",
                            @default = 1
                        }
                    },
                    required = new[] { "longitude", "latitude" }
                }
            },
            new McpToolDefinition
            {
                Name = "pdok_find_address",
                Description = "Search for Dutch addresses by postal code or by street and city name.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        postalCode = new
                        {
                            type = "string",
                            description = "Dutch postal code (e.g., '1012AA')"
                        },
                        houseNumber = new
                        {
                            type = "integer",
                            description = "Optional house number"
                        },
                        street = new
                        {
                            type = "string",
                            description = "Street name (required if no postal code provided)"
                        },
                        city = new
                        {
                            type = "string",
                            description = "City name (required if no postal code provided)"
                        },
                        maxResults = new
                        {
                            type = "integer",
                            description = "Maximum number of results (default: 100)",
                            @default = 100
                        }
                    }
                }
            },
            new McpToolDefinition
            {
                Name = "pdok_find_building",
                Description = "Get information about buildings (Panden) near a location from the BAG registry.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        longitude = new
                        {
                            type = "number",
                            description = "Longitude in WGS84"
                        },
                        latitude = new
                        {
                            type = "number",
                            description = "Latitude in WGS84"
                        },
                        radiusMeters = new
                        {
                            type = "number",
                            description = "Search radius in meters (default: 100)",
                            @default = 100
                        },
                        maxResults = new
                        {
                            type = "integer",
                            description = "Maximum number of results (default: 10)",
                            @default = 10
                        }
                    },
                    required = new[] { "longitude", "latitude" }
                }
            },
            new McpToolDefinition
            {
                Name = "pdok_find_cadastral_parcel",
                Description = "Find the cadastral parcel (perceel) at a specific location from the Kadaster.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        longitude = new
                        {
                            type = "number",
                            description = "Longitude in WGS84"
                        },
                        latitude = new
                        {
                            type = "number",
                            description = "Latitude in WGS84"
                        }
                    },
                    required = new[] { "longitude", "latitude" }
                }
            },
            new McpToolDefinition
            {
                Name = "pdok_get_cadastral_info",
                Description = "Get detailed cadastral information (parcels and boundaries) for an area.",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        longitude = new
                        {
                            type = "number",
                            description = "Longitude in WGS84"
                        },
                        latitude = new
                        {
                            type = "number",
                            description = "Latitude in WGS84"
                        },
                        radiusMeters = new
                        {
                            type = "number",
                            description = "Search radius in meters (default: 500)",
                            @default = 500
                        },
                        maxParcels = new
                        {
                            type = "integer",
                            description = "Maximum number of parcels (default: 10)",
                            @default = 10
                        }
                    },
                    required = new[] { "longitude", "latitude" }
                }
            }
        };
    }

    #endregion

    public void Dispose()
    {
        _bagClient?.Dispose();
        _kadastraleKaartClient?.Dispose();
        _kadastralePercelenClient?.Dispose();
        _locatieserverClient?.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// MCP Tool Definition for protocol registration
/// </summary>
public class McpToolDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public object InputSchema { get; set; } = new { };
}
