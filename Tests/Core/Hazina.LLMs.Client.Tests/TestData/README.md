# TestData Folder

This folder contains test data files used by unit and integration tests.

## Naming Convention

- `sample_*.json` - Sample data for various test scenarios
- `valid_*.json` - Valid input data for positive tests
- `invalid_*.json` - Invalid/malformed data for negative tests
- `expected_*.json` - Expected output data for assertions

## Usage in Tests

```csharp
public class MyTests
{
    private static string GetTestDataPath(string filename)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var assemblyDir = Path.GetDirectoryName(assembly.Location);
        return Path.Combine(assemblyDir!, "TestData", filename);
    }

    [Fact]
    public void LoadTestData_ShouldWork()
    {
        var json = File.ReadAllText(GetTestDataPath("sample_messages.json"));
        var data = JsonSerializer.Deserialize<TestDataModel>(json);
        // Use data in tests
    }
}
```

## Project File Configuration

Ensure test data files are copied to output:

```xml
<ItemGroup>
  <None Update="TestData\**\*">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```
