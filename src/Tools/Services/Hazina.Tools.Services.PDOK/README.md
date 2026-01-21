# Hazina.Tools.Services.PDOK

Comprehensive integration for **PDOK** (Publieke Dienstverlening Op de Kaart) - the Dutch government's platform for high-quality open geospatial data.

This library provides C# clients for accessing all major PDOK services including BAG (addresses & buildings), BRK (cadastral data), BGT (topography), and geocoding services. It also includes an **MCP (Model Context Protocol) server** to make Dutch geospatial data accessible to AI agents like Claude.

## 🌍 What is PDOK?

PDOK (Public Services on the Map) is the official Dutch government platform providing free access to high-quality geospatial data from Kadaster and other authorities. It serves over **87 million API calls per day** and provides standardized OGC-compliant APIs for:

- **BAG** (Basisregistratie Adressen en Gebouwen) - All Dutch addresses and buildings
- **BRK** (Basisregistratie Kadaster) - Cadastral boundaries and parcels
- **BGT** (Basisregistratie Grootschalige Topografie) - Large-scale topographic data
- **Locatieserver** - Free geocoding and reverse geocoding for the Netherlands

## 📦 Installation

```bash
dotnet add package Hazina.Tools.Services.PDOK
```

## 🚀 Quick Start

### Geocoding

```csharp
using Hazina.Tools.Services.PDOK.Clients;

// Find coordinates for an address
using var geocoder = new LocatieserverClient();
var results = await geocoder.GeocodeAsync("Nieuwe Gracht 1, Utrecht");

foreach (var result in results)
{
    Console.WriteLine($"{result.Weergavenaam}");
    Console.WriteLine($"Coordinates: {result.Latitude}, {result.Longitude}");
}
```

### Reverse Geocoding

```csharp
// Find address for coordinates
var results = await geocoder.ReverseGeocodeAsync(5.1214, 52.0907);
var address = results.First();
Console.WriteLine($"Address: {address.Weergavenaam}");
```

### Find Buildings (BAG)

```csharp
using var bagClient = new BagClient();

// Find buildings near a location
var buildings = await bagClient.FindPandenNearAsync(
    longitude: 5.1214,
    latitude: 52.0907,
    radiusMeters: 100,
    limit: 10
);

foreach (var feature in buildings.Features)
{
    var pand = feature.Properties;
    Console.WriteLine($"Building {pand.Identificatie}");
    Console.WriteLine($"Built: {pand.OorspronkelijkBouwjaar}");
    Console.WriteLine($"Status: {pand.Status}");
}
```

### Find Cadastral Parcel

```csharp
using var kadasterClient = new KadastraleKaartClient();

// Find which cadastral parcel a location belongs to
var parcels = await kadasterClient.FindPerceelAtLocationAsync(5.1214, 52.0907);

if (parcels.Features.Count > 0)
{
    var perceel = parcels.Features.First().Properties;
    Console.WriteLine($"Parcel: {perceel.Perceelnummer}");
    Console.WriteLine($"Municipality: {perceel.KadastraleGemeente}");
    Console.WriteLine($"Section: {perceel.Sectie}");
    Console.WriteLine($"Area: {perceel.Oppervlakte} m²");
}
```

## 🤖 MCP Server Integration

Use the `PdokMcpServer` to expose PDOK services as tools for AI agents:

```csharp
using Hazina.Tools.Services.PDOK.Mcp;

using var mcpServer = new PdokMcpServer();

// Geocode using MCP tool
var result = await mcpServer.GeocodeAsync("Dam, Amsterdam", maxResults: 5);
Console.WriteLine(result); // JSON response

// Reverse geocode
var address = await mcpServer.ReverseGeocodeAsync(4.8952, 52.3702);

// Find cadastral parcel
var parcel = await mcpServer.FindCadastralParcelAsync(4.8952, 52.3702);

// Get building information
var buildings = await mcpServer.FindBuildingAsync(4.8952, 52.3702, radiusMeters: 100);
```

### Available MCP Tools

The MCP server provides these tools for AI agents:

1. **`pdok_geocode`** - Find coordinates for Dutch address
   - Input: `query` (address string), `maxResults` (optional)
   - Output: List of addresses with coordinates

2. **`pdok_reverse_geocode`** - Find address for coordinates
   - Input: `longitude`, `latitude`, `maxResults` (optional)
   - Output: Nearest address(es)

3. **`pdok_find_address`** - Search addresses by postal code or street+city
   - Input: `postalCode` OR (`street` AND `city`), `houseNumber` (optional)
   - Output: List of matching addresses

4. **`pdok_find_building`** - Get building information
   - Input: `longitude`, `latitude`, `radiusMeters`, `maxResults`
   - Output: Buildings near location with BAG data

5. **`pdok_find_cadastral_parcel`** - Find cadastral parcel at location
   - Input: `longitude`, `latitude`
   - Output: Cadastral parcel information

6. **`pdok_get_cadastral_info`** - Get detailed cadastral info for area
   - Input: `longitude`, `latitude`, `radiusMeters`, `maxParcels`
   - Output: All parcels and boundaries in area

### Tool Definitions

```csharp
// Get MCP tool definitions for registration
var toolDefinitions = PdokMcpServer.GetToolDefinitions();

foreach (var tool in toolDefinitions)
{
    Console.WriteLine($"{tool.Name}: {tool.Description}");
}
```

## 📚 Available Clients

### 1. LocatieserverClient

Free geocoding service for the Netherlands.

**Methods:**
- `GeocodeAsync(query, type, maxResults)` - Geocode address/location
- `ReverseGeocodeAsync(lon, lat, type, maxResults)` - Reverse geocode
- `FindByPostalCodeAsync(postalCode, houseNumber, maxResults)` - Search by postal code
- `FindByStreetAndCityAsync(street, city, houseNumber, maxResults)` - Search by street+city
- `SuggestAsync(partialQuery, maxResults)` - Autocomplete suggestions
- `LookupAsync(id)` - Get object by ID

**Supported Types:**
- `LocatieserverTypes.Address` - Addresses
- `LocatieserverTypes.PostalCode` - Postal codes
- `LocatieserverTypes.Street` - Streets
- `LocatieserverTypes.City` - Cities
- `LocatieserverTypes.Municipality` - Municipalities
- `LocatieserverTypes.Province` - Provinces
- `LocatieserverTypes.Parcel` - Cadastral parcels

### 2. BagClient

Access to BAG (Basisregistratie Adressen en Gebouwen) - official Dutch address and building registry.

**Collections:**
- `verblijfsobject` - Residential objects (addresses)
- `pand` - Buildings
- `nummeraanduiding` - Address designations
- `openbareruimte` - Public spaces (streets)
- `woonplaats` - Cities/towns

**Key Methods:**
- `FindVerblijfsobjectenAsync(minLon, minLat, maxLon, maxLat, limit)` - Find addresses in bounding box
- `FindVerblijfsobjectenNearAsync(lon, lat, radiusMeters, limit)` - Find addresses near point
- `GetVerblijfsobjectAsync(bagId)` - Get specific address by BAG ID
- `FindPandenAsync(...)` / `FindPandenNearAsync(...)` - Find buildings
- `FindNummeraanduidingenByPostcodeAsync(postcode, limit)` - Find by postal code
- `FindOpenbareRuimtenAsync(naam, limit)` - Find streets by name
- `FindWoonplaatsenAsync(naam, limit)` - Find cities by name

### 3. KadastraleKaartClient

Access to cadastral map (Kadastrale Kaart) with parcels, boundaries, and map features.

**Collections:**
- `kadastralegrenzen` - Cadastral boundaries
- `perceel` - Cadastral parcels
- `openbareruimtenaam` - Public space names
- `bebouwing` - Buildings
- `nummeraanduidingreeks` - House number ranges

**Key Methods:**
- `FindKadastraleGrenzenAsync(minLon, minLat, maxLon, maxLat, limit)` - Find boundaries
- `FindPercelenAsync(...)` / `FindPerceelAtLocationAsync(lon, lat)` - Find parcels
- `GetPerceelAsync(perceelId)` - Get specific parcel
- `FindPercelenByGemeenteAndSectieAsync(gemeenteCode, sectie, limit)` - Find by municipality+section

### 4. KadastralePercelenClient

Access to INSPIRE-harmonized cadastral parcels (international standard).

**Key Methods:**
- `FindCadastralParcelsAsync(minLon, minLat, maxLon, maxLat, limit)` - Find parcels in bounding box
- `FindCadastralParcelAtLocationAsync(lon, lat)` - Find parcel at point
- `FindCadastralParcelsNearAsync(lon, lat, radiusMeters, limit)` - Find parcels near point
- `GetCadastralParcelAsync(parcelId)` - Get specific parcel

## 🗺️ Coordinate Systems

PDOK supports multiple coordinate reference systems:

- **WGS84** (GPS coordinates) - `EPSG:4326` / `CRS84` - Used by default
- **RD New** (Dutch National Grid) - `EPSG:28992` - Official Dutch system
- **Web Mercator** - `EPSG:3857` - Web mapping
- **ETRS89** - `EPSG:4258` - European standard

```csharp
using static Hazina.Tools.Services.PDOK.Models.CoordinateReferenceSystems;

// Request data in Dutch RD New coordinates
var results = await bagClient.QueryFeaturesAsync<Pand>(
    "pand",
    limit: 100,
    bbox: new List<double> { 155000, 463000, 156000, 464000 },
    crs: RdNew
);
```

## 📖 Data Models

All PDOK responses are strongly-typed with comprehensive C# models:

**BAG Models:**
- `Verblijfsobject` - Residential object (address)
- `Pand` - Building
- `Nummeraanduiding` - Address designation
- `OpenbareRuimte` - Public space (street)
- `Woonplaats` - City/town
- `Address` - Simplified address for common queries

**Cadastral Models:**
- `KadastraleGrens` - Cadastral boundary
- `Perceel` - Cadastral parcel
- `OpenbareRuimteNaam` - Public space name
- `Bebouwing` - Building on cadastral map
- `Nummeraanduidingreeks` - House number range
- `InspireCadastralParcel` - INSPIRE standard parcel

**Geocoding Models:**
- `LocatieserverDocument` - Geocoding result with address details

**GeoJSON Models:**
- `FeatureCollection<T>` - Collection of features
- `Feature<T>` - Single feature with geometry and properties
- `Collection` - OGC API collection metadata

## 🌐 OGC API Features

All clients extend `OgcApiClient` which implements the OGC API Features standard:

```csharp
// Base client functionality
public abstract class OgcApiClient
{
    Task<Collections> GetCollectionsAsync()
    Task<Collection> GetCollectionAsync(string collectionId)
    Task<FeatureCollection<T>> QueryFeaturesAsync<T>(...)
    Task<Feature<T>> GetFeatureAsync<T>(...)
    List<double> CreateBbox(...)
    List<double> CreateBboxAroundPoint(...)
}
```

## 🎯 Use Cases

### Real Estate & Property

```csharp
// Find all information about a property
var geocoder = new LocatieserverClient();
var bagClient = new BagClient();
var kadasterClient = new KadastraleKaartClient();

// 1. Geocode address
var addresses = await geocoder.GeocodeAsync("Nieuwe Gracht 1, Utrecht");
var location = addresses.First();

// 2. Get building information
var buildings = await bagClient.FindPandenNearAsync(
    location.Longitude.Value,
    location.Latitude.Value,
    radiusMeters: 10
);

// 3. Get cadastral parcel
var parcel = await kadasterClient.FindPerceelAtLocationAsync(
    location.Longitude.Value,
    location.Latitude.Value
);

Console.WriteLine($"Address: {location.Weergavenaam}");
Console.WriteLine($"Building Year: {buildings.Features.First().Properties.OorspronkelijkBouwjaar}");
Console.WriteLine($"Parcel Size: {parcel.Features.First().Properties.Oppervlakte} m²");
```

### Location Intelligence

```csharp
// Analyze an area
var mcpServer = new PdokMcpServer();

var info = await mcpServer.GetCadastralInfoAsync(
    longitude: 5.1214,
    latitude: 52.0907,
    radiusMeters: 1000,
    maxParcels: 50
);

// Returns JSON with all parcels and boundaries in the area
```

### Address Validation

```csharp
// Validate and normalize Dutch addresses
var geocoder = new LocatieserverClient();

var results = await geocoder.GeocodeAsync("nieuwe gracht 1 utrecht");
if (results.Count > 0)
{
    var validated = results.First();
    Console.WriteLine($"Normalized: {validated.Weergavenaam}");
    Console.WriteLine($"Postal Code: {validated.Postcode}");
    Console.WriteLine($"BAG ID: {validated.NummeraanduidingId}");
}
```

## 🔧 Advanced Usage

### Custom HttpClient

```csharp
var httpClient = new HttpClient
{
    Timeout = TimeSpan.FromSeconds(30)
};

var bagClient = new BagClient(httpClient);
var geocoder = new LocatieserverClient(httpClient);
```

### Custom Base URL (for testing or proxies)

```csharp
var bagClient = new BagClient("https://my-proxy.com/bag/v2");
```

### Spatial Filtering

```csharp
// Find all addresses in a bounding box
var bbox = OgcApiClient.CreateBbox(
    minLon: 5.0,
    minLat: 52.0,
    maxLon: 5.2,
    maxLat: 52.2
);

var addresses = await bagClient.FindVerblijfsobjectenAsync(5.0, 52.0, 5.2, 52.2, limit: 1000);
```

### Pagination

```csharp
// Get next page of results
var page1 = await bagClient.QueryFeaturesAsync<Pand>("pand", limit: 100, offset: 0);
var page2 = await bagClient.QueryFeaturesAsync<Pand>("pand", limit: 100, offset: 100);
var page3 = await bagClient.QueryFeaturesAsync<Pand>("pand", limit: 100, offset: 200);
```

## 📊 Response Format

All MCP tools return JSON responses:

```json
{
  "success": true,
  "result": {
    "address": "Dam 1, 1012JS Amsterdam",
    "longitude": 4.89333,
    "latitude": 52.37305,
    "city": "Amsterdam",
    "municipality": "Amsterdam",
    "postalCode": "1012JS"
  }
}
```

Error responses:

```json
{
  "success": false,
  "error": "No cadastral parcel found at this location"
}
```

## 🎓 Examples

See `Hazina.Demo.PDOK` for comprehensive examples including:

1. Geocoding and reverse geocoding
2. Finding addresses by postal code
3. Finding buildings near a location
4. Finding cadastral parcels
5. Using the MCP server
6. Combining multiple services

## 📝 License

This library uses PDOK's open data which is available under **CC BY 4.0** license (Creative Commons Attribution 4.0 International). You are free to use, modify, and distribute the data with attribution.

**Attribution:** "Contains data from PDOK (Publieke Dienstverlening Op de Kaart) - Kadaster"

## 🔗 Resources

- **PDOK Website:** https://www.pdok.nl
- **PDOK API Docs:** https://api.pdok.nl
- **BAG API:** https://api.pdok.nl/kadaster/bag/ogc/v2/
- **Kadastrale Kaart API:** https://api.pdok.nl/kadaster/brk-kadastrale-kaart/ogc/v1
- **Kadastrale Percelen API:** https://api.pdok.nl/kadaster/brk-kadastrale-percelen/ogc/v1
- **Locatieserver API:** https://api.pdok.nl/bzk/locatieserver/search/v3_1/
- **OGC API Features Standard:** https://ogcapi.ogc.org/features/

## 🤝 Contributing

Contributions are welcome! This library covers the core PDOK services, but additional services can be added:

- **BRT** (Basisregistratie Topografie) - Topographic base map
- **BGT** (Basisregistratie Grootschalige Topografie) - Large-scale topography
- **AHN** (Actueel Hoogtebestand Nederland) - Digital elevation model
- **CBS** (Centraal Bureau voor de Statistiek) - Statistical data

## 📈 Performance

PDOK APIs are production-ready and serve millions of requests daily:

- **Availability:** 99.9%+
- **Update Frequency:** Daily (cadastral), Real-time (some datasets)
- **Rate Limits:** None (fair use policy)
- **Caching:** Recommended for production applications

## ⚠️ Important Notes

1. **Coordinate Order:** PDOK uses longitude, latitude order (not lat, lon)
2. **Postal Codes:** Dutch postal codes have format "1234AB" (no space)
3. **BAG IDs:** Identifiers are unique and persistent
4. **Historical Data:** Available through `beginGeldigheid`/`eindGeldigheid` fields
5. **Geometry:** All geometries are in GeoJSON format using NetTopologySuite

## 💡 Tips

- Use `LocatieserverClient` for simple geocoding (fastest)
- Use `BagClient` when you need official BAG identifiers
- Use `KadastraleKaartClient` for cadastral map visualizations
- Use `KadastralePercelenClient` for INSPIRE-compliant data exchange
- Combine services for comprehensive property information
- Cache results to reduce API calls
- Use bounding boxes instead of large radius searches for better performance

---

**Built with ❤️ for the Hazina Framework**

*Making Dutch government geospatial data accessible to AI agents and applications*
