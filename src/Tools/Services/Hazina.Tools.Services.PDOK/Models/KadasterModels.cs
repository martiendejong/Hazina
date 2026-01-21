using System.Text.Json.Serialization;

namespace Hazina.Tools.Services.PDOK.Models;

/// <summary>
/// Kadastrale Grens (Cadastral boundary)
/// </summary>
public class KadastraleGrens
{
    [JsonPropertyName("identificatie")]
    public string? Identificatie { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("beginGeldigheid")]
    public DateTime? BeginGeldigheid { get; set; }

    [JsonPropertyName("eindGeldigheid")]
    public DateTime? EindGeldigheid { get; set; }
}

/// <summary>
/// Perceel (Cadastral parcel)
/// </summary>
public class Perceel
{
    [JsonPropertyName("identificatie")]
    public string? Identificatie { get; set; }

    [JsonPropertyName("perceelnummer")]
    public string? Perceelnummer { get; set; }

    [JsonPropertyName("akrAanduiding")]
    public string? AkrAanduiding { get; set; }

    [JsonPropertyName("kadastraleGemeente")]
    public string? KadastraleGemeente { get; set; }

    [JsonPropertyName("kadastraleGemeenteCode")]
    public string? KadastraleGemeenteCode { get; set; }

    [JsonPropertyName("sectie")]
    public string? Sectie { get; set; }

    [JsonPropertyName("perceelnummerRotatie")]
    public double? PerceelnummerRotatie { get; set; }

    [JsonPropertyName("plaatscoördinaten")]
    public PlaatscoordinatenType? Plaatscoordinaten { get; set; }

    [JsonPropertyName("oppervlakte")]
    public int? Oppervlakte { get; set; }

    [JsonPropertyName("beginGeldigheid")]
    public DateTime? BeginGeldigheid { get; set; }

    [JsonPropertyName("eindGeldigheid")]
    public DateTime? EindGeldigheid { get; set; }
}

public class PlaatscoordinatenType
{
    [JsonPropertyName("x")]
    public double? X { get; set; }

    [JsonPropertyName("y")]
    public double? Y { get; set; }
}

/// <summary>
/// Openbare Ruimte Naam (Public space name on cadastral map)
/// </summary>
public class OpenbareRuimteNaam
{
    [JsonPropertyName("identificatie")]
    public string? Identificatie { get; set; }

    [JsonPropertyName("openbareRuimteNaam")]
    public string? Naam { get; set; }

    [JsonPropertyName("naamRotatie")]
    public double? NaamRotatie { get; set; }

    [JsonPropertyName("plaatscoördinaten")]
    public PlaatscoordinatenType? Plaatscoordinaten { get; set; }
}

/// <summary>
/// Bebouwing (Building on cadastral map)
/// </summary>
public class Bebouwing
{
    [JsonPropertyName("identificatie")]
    public string? Identificatie { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}

/// <summary>
/// Nummeraanduidingreeks (Range of house numbers on cadastral map)
/// </summary>
public class Nummeraanduidingreeks
{
    [JsonPropertyName("identificatie")]
    public string? Identificatie { get; set; }

    [JsonPropertyName("nummeraanduidingreeks")]
    public string? Reeks { get; set; }

    [JsonPropertyName("naamRotatie")]
    public double? NaamRotatie { get; set; }

    [JsonPropertyName("plaatscoördinaten")]
    public PlaatscoordinatenType? Plaatscoordinaten { get; set; }
}

/// <summary>
/// INSPIRE Cadastral Parcel (international standard format)
/// </summary>
public class InspireCadastralParcel
{
    [JsonPropertyName("inspireId")]
    public InspireId? InspireId { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("nationalCadastralReference")]
    public string? NationalCadastralReference { get; set; }

    [JsonPropertyName("areaValue")]
    public double? AreaValue { get; set; }

    [JsonPropertyName("beginLifespanVersion")]
    public DateTime? BeginLifespanVersion { get; set; }

    [JsonPropertyName("endLifespanVersion")]
    public DateTime? EndLifespanVersion { get; set; }

    [JsonPropertyName("validFrom")]
    public DateTime? ValidFrom { get; set; }

    [JsonPropertyName("validTo")]
    public DateTime? ValidTo { get; set; }
}

public class InspireId
{
    [JsonPropertyName("localId")]
    public string? LocalId { get; set; }

    [JsonPropertyName("namespace")]
    public string? Namespace { get; set; }
}
