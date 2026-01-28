using System.Text.Json.Serialization;
using NetTopologySuite.Geometries;

namespace Hazina.Tools.Services.PDOK.Models;

/// <summary>
/// GeoJSON Feature Collection (OGC API Features response)
/// </summary>
public class FeatureCollection<T> where T : class
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "FeatureCollection";

    [JsonPropertyName("features")]
    public List<Feature<T>> Features { get; set; } = new();

    [JsonPropertyName("numberMatched")]
    public int? NumberMatched { get; set; }

    [JsonPropertyName("numberReturned")]
    public int? NumberReturned { get; set; }

    [JsonPropertyName("links")]
    public List<Link> Links { get; set; } = new();

    [JsonPropertyName("timeStamp")]
    public DateTime? TimeStamp { get; set; }
}

/// <summary>
/// GeoJSON Feature
/// </summary>
public class Feature<T> where T : class
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "Feature";

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("geometry")]
    public Geometry? Geometry { get; set; }

    [JsonPropertyName("properties")]
    public T? Properties { get; set; }

    [JsonPropertyName("links")]
    public List<Link>? Links { get; set; }
}

/// <summary>
/// HAL-style link
/// </summary>
public class Link
{
    [JsonPropertyName("href")]
    public string Href { get; set; } = string.Empty;

    [JsonPropertyName("rel")]
    public string? Rel { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("hreflang")]
    public string? Hreflang { get; set; }
}

/// <summary>
/// OGC API Collections response
/// </summary>
public class Collections
{
    [JsonPropertyName("collections")]
    public List<Collection> CollectionList { get; set; } = new();

    [JsonPropertyName("links")]
    public List<Link> Links { get; set; } = new();
}

/// <summary>
/// OGC API Collection metadata
/// </summary>
public class Collection
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("links")]
    public List<Link> Links { get; set; } = new();

    [JsonPropertyName("extent")]
    public Extent? Extent { get; set; }

    [JsonPropertyName("itemType")]
    public string? ItemType { get; set; }

    [JsonPropertyName("crs")]
    public List<string>? Crs { get; set; }
}

/// <summary>
/// Spatial and temporal extent
/// </summary>
public class Extent
{
    [JsonPropertyName("spatial")]
    public SpatialExtent? Spatial { get; set; }

    [JsonPropertyName("temporal")]
    public TemporalExtent? Temporal { get; set; }
}

public class SpatialExtent
{
    [JsonPropertyName("bbox")]
    public List<List<double>> Bbox { get; set; } = new();

    [JsonPropertyName("crs")]
    public string? Crs { get; set; }
}

public class TemporalExtent
{
    [JsonPropertyName("interval")]
    public List<List<DateTime?>> Interval { get; set; } = new();

    [JsonPropertyName("trs")]
    public string? Trs { get; set; }
}

/// <summary>
/// Common coordinate reference systems used in PDOK
/// </summary>
public static class CoordinateReferenceSystems
{
    /// <summary>
    /// WGS84 (GPS coordinates) - EPSG:4326
    /// </summary>
    public const string Wgs84 = "http://www.opengis.net/def/crs/OGC/1.3/CRS84";

    /// <summary>
    /// Amersfoort / RD New (Dutch National Grid) - EPSG:28992
    /// </summary>
    public const string RdNew = "http://www.opengis.net/def/crs/EPSG/0/28992";

    /// <summary>
    /// Web Mercator - EPSG:3857
    /// </summary>
    public const string WebMercator = "http://www.opengis.net/def/crs/EPSG/0/3857";

    /// <summary>
    /// ETRS89 (European standard) - EPSG:4258
    /// </summary>
    public const string Etrs89 = "http://www.opengis.net/def/crs/EPSG/0/4258";
}
