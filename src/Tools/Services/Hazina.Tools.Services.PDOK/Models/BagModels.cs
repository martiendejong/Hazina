using System.Text.Json.Serialization;

namespace Hazina.Tools.Services.PDOK.Models;

/// <summary>
/// BAG Verblijfsobject (Residential object / Address)
/// Represents a habitable or usable space within a building
/// </summary>
public class Verblijfsobject
{
    [JsonPropertyName("identificatie")]
    public string? Identificatie { get; set; }

    [JsonPropertyName("domein")]
    public string? Domein { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("oppervlakte")]
    public int? Oppervlakte { get; set; }

    [JsonPropertyName("gebruiksdoel")]
    public List<string>? Gebruiksdoel { get; set; }

    [JsonPropertyName("pandIdentificatie")]
    public string? PandIdentificatie { get; set; }

    [JsonPropertyName("nummeraanduidingIdentificatie")]
    public string? NummeraanduidingIdentificatie { get; set; }

    [JsonPropertyName("documentdatum")]
    public DateTime? Documentdatum { get; set; }

    [JsonPropertyName("documentnummer")]
    public string? Documentnummer { get; set; }

    [JsonPropertyName("voorkomenidentificatie")]
    public int? Voorkomenidentificatie { get; set; }

    [JsonPropertyName("beginGeldigheid")]
    public DateTime? BeginGeldigheid { get; set; }

    [JsonPropertyName("eindGeldigheid")]
    public DateTime? EindGeldigheid { get; set; }

    [JsonPropertyName("tijdstipRegistratie")]
    public DateTime? TijdstipRegistratie { get; set; }

    [JsonPropertyName("eindRegistratie")]
    public DateTime? EindRegistratie { get; set; }
}

/// <summary>
/// BAG Pand (Building)
/// Physical building structure
/// </summary>
public class Pand
{
    [JsonPropertyName("identificatie")]
    public string? Identificatie { get; set; }

    [JsonPropertyName("domein")]
    public string? Domein { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("oorspronkelijkBouwjaar")]
    public int? OorspronkelijkBouwjaar { get; set; }

    [JsonPropertyName("documentdatum")]
    public DateTime? Documentdatum { get; set; }

    [JsonPropertyName("documentnummer")]
    public string? Documentnummer { get; set; }

    [JsonPropertyName("voorkomenidentificatie")]
    public int? Voorkomenidentificatie { get; set; }

    [JsonPropertyName("beginGeldigheid")]
    public DateTime? BeginGeldigheid { get; set; }

    [JsonPropertyName("eindGeldigheid")]
    public DateTime? EindGeldigheid { get; set; }

    [JsonPropertyName("tijdstipRegistratie")]
    public DateTime? TijdstipRegistratie { get; set; }

    [JsonPropertyName("eindRegistratie")]
    public DateTime? EindRegistratie { get; set; }
}

/// <summary>
/// BAG Nummeraanduiding (Address designation)
/// The official address number, letter, and addition
/// </summary>
public class Nummeraanduiding
{
    [JsonPropertyName("identificatie")]
    public string? Identificatie { get; set; }

    [JsonPropertyName("domein")]
    public string? Domein { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("huisnummer")]
    public int? Huisnummer { get; set; }

    [JsonPropertyName("huisletter")]
    public string? Huisletter { get; set; }

    [JsonPropertyName("huisnummertoevoeging")]
    public string? Huisnummertoevoeging { get; set; }

    [JsonPropertyName("postcode")]
    public string? Postcode { get; set; }

    [JsonPropertyName("typeAdresseerbaarObject")]
    public string? TypeAdresseerbaarObject { get; set; }

    [JsonPropertyName("openbareRuimteIdentificatie")]
    public string? OpenbareRuimteIdentificatie { get; set; }

    [JsonPropertyName("woonplaatsIdentificatie")]
    public string? WoonplaatsIdentificatie { get; set; }

    [JsonPropertyName("documentdatum")]
    public DateTime? Documentdatum { get; set; }

    [JsonPropertyName("documentnummer")]
    public string? Documentnummer { get; set; }

    [JsonPropertyName("voorkomenidentificatie")]
    public int? Voorkomenidentificatie { get; set; }

    [JsonPropertyName("beginGeldigheid")]
    public DateTime? BeginGeldigheid { get; set; }

    [JsonPropertyName("eindGeldigheid")]
    public DateTime? EindGeldigheid { get; set; }

    [JsonPropertyName("tijdstipRegistratie")]
    public DateTime? TijdstipRegistratie { get; set; }

    [JsonPropertyName("eindRegistratie")]
    public DateTime? EindRegistratie { get; set; }
}

/// <summary>
/// BAG Openbare Ruimte (Public space / Street)
/// </summary>
public class OpenbareRuimte
{
    [JsonPropertyName("identificatie")]
    public string? Identificatie { get; set; }

    [JsonPropertyName("domein")]
    public string? Domein { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("naam")]
    public string? Naam { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("woonplaatsIdentificatie")]
    public string? WoonplaatsIdentificatie { get; set; }

    [JsonPropertyName("documentdatum")]
    public DateTime? Documentdatum { get; set; }

    [JsonPropertyName("documentnummer")]
    public string? Documentnummer { get; set; }

    [JsonPropertyName("voorkomenidentificatie")]
    public int? Voorkomenidentificatie { get; set; }

    [JsonPropertyName("beginGeldigheid")]
    public DateTime? BeginGeldigheid { get; set; }

    [JsonPropertyName("eindGeldigheid")]
    public DateTime? EindGeldigheid { get; set; }

    [JsonPropertyName("tijdstipRegistratie")]
    public DateTime? TijdstipRegistratie { get; set; }

    [JsonPropertyName("eindRegistratie")]
    public DateTime? EindRegistratie { get; set; }
}

/// <summary>
/// BAG Woonplaats (City/Town)
/// </summary>
public class Woonplaats
{
    [JsonPropertyName("identificatie")]
    public string? Identificatie { get; set; }

    [JsonPropertyName("domein")]
    public string? Domein { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("naam")]
    public string? Naam { get; set; }

    [JsonPropertyName("documentdatum")]
    public DateTime? Documentdatum { get; set; }

    [JsonPropertyName("documentnummer")]
    public string? Documentnummer { get; set; }

    [JsonPropertyName("voorkomenidentificatie")]
    public int? Voorkomenidentificatie { get; set; }

    [JsonPropertyName("beginGeldigheid")]
    public DateTime? BeginGeldigheid { get; set; }

    [JsonPropertyName("eindGeldigheid")]
    public DateTime? EindGeldigheid { get; set; }

    [JsonPropertyName("tijdstipRegistratie")]
    public DateTime? TijdstipRegistratie { get; set; }

    [JsonPropertyName("eindRegistratie")]
    public DateTime? EindRegistratie { get; set; }
}

/// <summary>
/// Simplified address result for common queries
/// </summary>
public class Address
{
    public string? Street { get; set; }
    public int? HouseNumber { get; set; }
    public string? HouseLetter { get; set; }
    public string? HouseNumberAddition { get; set; }
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    public string? Municipality { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? BagId { get; set; }
}
