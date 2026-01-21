using System.Text.Json.Serialization;

namespace Hazina.Tools.Services.PDOK.Models;

/// <summary>
/// PDOK Locatieserver geocoding response
/// </summary>
public class LocatieserverResponse
{
    [JsonPropertyName("response")]
    public LocatieserverResponseBody? Response { get; set; }
}

public class LocatieserverResponseBody
{
    [JsonPropertyName("numFound")]
    public int NumFound { get; set; }

    [JsonPropertyName("start")]
    public int Start { get; set; }

    [JsonPropertyName("maxScore")]
    public double? MaxScore { get; set; }

    [JsonPropertyName("docs")]
    public List<LocatieserverDocument> Docs { get; set; } = new();
}

/// <summary>
/// Geocoding result document
/// </summary>
public class LocatieserverDocument
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("weergavenaam")]
    public string? Weergavenaam { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("score")]
    public double? Score { get; set; }

    // Address components
    [JsonPropertyName("woonplaatsnaam")]
    public string? Woonplaatsnaam { get; set; }

    [JsonPropertyName("huis_nlt")]
    public string? HuisNlt { get; set; }

    [JsonPropertyName("openbareruimtenaam")]
    public string? Openbareruimtenaam { get; set; }

    [JsonPropertyName("postcode")]
    public string? Postcode { get; set; }

    [JsonPropertyName("gemeentenaam")]
    public string? Gemeentenaam { get; set; }

    [JsonPropertyName("provincienaam")]
    public string? Provincienaam { get; set; }

    // Coordinates
    [JsonPropertyName("centroide_ll")]
    public string? CentroideLl { get; set; }

    [JsonPropertyName("centroide_rd")]
    public string? CentroideRd { get; set; }

    // Numeric coordinates (parsed from centroide_ll: "POINT(lon lat)")
    public double? Longitude { get; set; }
    public double? Latitude { get; set; }

    // BAG/BRK identifiers
    [JsonPropertyName("nummeraanduiding_id")]
    public string? NummeraanduidingId { get; set; }

    [JsonPropertyName("adresseerbaarobject_id")]
    public string? AdresseerbaarobjectId { get; set; }

    [JsonPropertyName("openbareruimte_id")]
    public string? OpenbareruimteId { get; set; }

    [JsonPropertyName("woonplaats_id")]
    public string? WoonplaatsId { get; set; }
}

/// <summary>
/// Reverse geocoding request
/// </summary>
public class ReverseGeocodeRequest
{
    public double Longitude { get; set; }
    public double Latitude { get; set; }
    public int? MaxResults { get; set; } = 1;
    public double? RadiusMeters { get; set; }
}

/// <summary>
/// Geocoding request
/// </summary>
public class GeocodeRequest
{
    public string? Query { get; set; }
    public string? Type { get; set; }
    public int? MaxResults { get; set; } = 10;
    public string? FilterField { get; set; }
    public string? FilterValue { get; set; }
}

/// <summary>
/// Supported location types for filtering
/// </summary>
public static class LocatieserverTypes
{
    public const string Address = "adres";
    public const string PostalCode = "postcode";
    public const string Street = "weg";
    public const string City = "woonplaats";
    public const string Municipality = "gemeente";
    public const string Province = "provincie";
    public const string Parcel = "perceel";
}
