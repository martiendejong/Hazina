using Hazina.Tools.Services.PDOK.Clients;
using Hazina.Tools.Services.PDOK.Mcp;
using Hazina.Tools.Services.PDOK.Models;

Console.WriteLine("===================================");
Console.WriteLine("   PDOK Integration Demo");
Console.WriteLine("   Dutch Kadaster Open Data");
Console.WriteLine("===================================\n");

// Example 1: Geocoding
await GeocodeExample();

// Example 2: Reverse Geocoding
await ReverseGeocodeExample();

// Example 3: Find Address by Postal Code
await FindAddressByPostalCodeExample();

// Example 4: Find Buildings
await FindBuildingsExample();

// Example 5: Find Cadastral Parcel
await FindCadastralParcelExample();

// Example 6: MCP Server Usage
await McpServerExample();

Console.WriteLine("\n===================================");
Console.WriteLine("   Demo Complete!");
Console.WriteLine("===================================");

#region Example Functions

static async Task GeocodeExample()
{
    Console.WriteLine("--- Example 1: Geocoding ---");
    Console.WriteLine("Finding coordinates for: 'Nieuwe Gracht 1, Utrecht'\n");

    using var client = new LocatieserverClient();
    var results = await client.GeocodeAsync("Nieuwe Gracht 1, Utrecht", maxResults: 5);

    foreach (var result in results)
    {
        Console.WriteLine($"  Address: {result.Weergavenaam}");
        Console.WriteLine($"  Coordinates: {result.Latitude:F6}, {result.Longitude:F6}");
        Console.WriteLine($"  City: {result.Woonplaatsnaam}");
        Console.WriteLine($"  Type: {result.Type}");
        Console.WriteLine($"  Score: {result.Score:F2}");
        Console.WriteLine();
    }
}

static async Task ReverseGeocodeExample()
{
    Console.WriteLine("--- Example 2: Reverse Geocoding ---");
    Console.WriteLine("Finding address for coordinates: 52.0907, 5.1214 (Utrecht)\n");

    using var client = new LocatieserverClient();
    var results = await client.ReverseGeocodeAsync(5.1214, 52.0907);

    if (results.Count > 0)
    {
        var result = results.First();
        Console.WriteLine($"  Nearest Address: {result.Weergavenaam}");
        Console.WriteLine($"  Postal Code: {result.Postcode}");
        Console.WriteLine($"  City: {result.Woonplaatsnaam}");
        Console.WriteLine($"  Municipality: {result.Gemeentenaam}");
        Console.WriteLine();
    }
}

static async Task FindAddressByPostalCodeExample()
{
    Console.WriteLine("--- Example 3: Find Address by Postal Code ---");
    Console.WriteLine("Searching for postal code: 1012AA\n");

    using var client = new LocatieserverClient();
    var results = await client.FindByPostalCodeAsync("1012AA", maxResults: 5);

    foreach (var result in results)
    {
        Console.WriteLine($"  Address: {result.Weergavenaam}");
        Console.WriteLine($"  Coordinates: {result.Latitude:F6}, {result.Longitude:F6}");
        Console.WriteLine();
    }
}

static async Task FindBuildingsExample()
{
    Console.WriteLine("--- Example 4: Find Buildings (BAG Panden) ---");
    Console.WriteLine("Searching for buildings near Utrecht city center\n");

    using var client = new BagClient();
    var results = await client.FindPandenNearAsync(5.1214, 52.0907, radiusMeters: 200, limit: 5);

    Console.WriteLine($"  Found {results.Features.Count} buildings");
    foreach (var feature in results.Features.Take(3))
    {
        var pand = feature.Properties;
        Console.WriteLine($"  BAG ID: {pand?.Identificatie}");
        Console.WriteLine($"  Status: {pand?.Status}");
        Console.WriteLine($"  Build Year: {pand?.OorspronkelijkBouwjaar}");
        Console.WriteLine($"  Geometry Type: {feature.Geometry?.GeometryType}");
        Console.WriteLine();
    }
}

static async Task FindCadastralParcelExample()
{
    Console.WriteLine("--- Example 5: Find Cadastral Parcel ---");
    Console.WriteLine("Finding cadastral parcel at Utrecht city center\n");

    using var client = new KadastraleKaartClient();
    var results = await client.FindPerceelAtLocationAsync(5.1214, 52.0907);

    if (results.Features.Count > 0)
    {
        var perceel = results.Features.First().Properties;
        Console.WriteLine($"  Parcel ID: {perceel?.Identificatie}");
        Console.WriteLine($"  Parcel Number: {perceel?.Perceelnummer}");
        Console.WriteLine($"  AKR Designation: {perceel?.AkrAanduiding}");
        Console.WriteLine($"  Cadastral Municipality: {perceel?.KadastraleGemeente}");
        Console.WriteLine($"  Section: {perceel?.Sectie}");
        Console.WriteLine($"  Area: {perceel?.Oppervlakte} m²");
        Console.WriteLine();
    }
    else
    {
        Console.WriteLine("  No cadastral parcel found at this location\n");
    }
}

static async Task McpServerExample()
{
    Console.WriteLine("--- Example 6: MCP Server Usage ---");
    Console.WriteLine("Using PDOK MCP Server to geocode address\n");

    using var mcpServer = new PdokMcpServer();

    // Example: Geocode using MCP tool
    var geocodeResult = await mcpServer.GeocodeAsync("Dam, Amsterdam", maxResults: 3);
    Console.WriteLine("  MCP Tool: pdok_geocode");
    Console.WriteLine($"  Result: {geocodeResult[..Math.Min(500, geocodeResult.Length)]}...");
    Console.WriteLine();

    // Example: Reverse geocode using MCP tool
    var reverseResult = await mcpServer.ReverseGeocodeAsync(4.8952, 52.3702); // Amsterdam center
    Console.WriteLine("  MCP Tool: pdok_reverse_geocode");
    Console.WriteLine($"  Result: {reverseResult[..Math.Min(300, reverseResult.Length)]}...");
    Console.WriteLine();

    // Example: Find cadastral parcel using MCP tool
    var cadastralResult = await mcpServer.FindCadastralParcelAsync(4.8952, 52.3702);
    Console.WriteLine("  MCP Tool: pdok_find_cadastral_parcel");
    Console.WriteLine($"  Result: {cadastralResult[..Math.Min(400, cadastralResult.Length)]}...");
    Console.WriteLine();

    // Show available MCP tools
    Console.WriteLine("  Available MCP Tools:");
    var tools = PdokMcpServer.GetToolDefinitions();
    foreach (var tool in tools)
    {
        Console.WriteLine($"    - {tool.Name}: {tool.Description}");
    }
    Console.WriteLine();
}

#endregion
