using System.Text.Json;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Hazina.API.Generic.Configuration;

/// <summary>
/// Loads entity definitions from YAML or JSON configuration files.
/// This allows defining entities without writing C# code.
/// </summary>
public class EntityConfigurationLoader
{
    private readonly IDeserializer _yamlDeserializer;
    private readonly JsonSerializerOptions _jsonOptions;

    public EntityConfigurationLoader()
    {
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }

    /// <summary>
    /// Loads entity definitions from a configuration file.
    /// Supports .yaml, .yml, and .json files.
    /// </summary>
    public async Task<List<EntityDefinition>> LoadFromFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Configuration file not found: {filePath}");
        }

        var content = await File.ReadAllTextAsync(filePath);
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".yaml" or ".yml" => LoadFromYaml(content),
            ".json" => LoadFromJson(content),
            _ => throw new NotSupportedException($"Unsupported file extension: {extension}")
        };
    }

    /// <summary>
    /// Loads entity definitions from a directory.
    /// Each file can contain one or more entity definitions.
    /// </summary>
    public async Task<List<EntityDefinition>> LoadFromDirectoryAsync(string directoryPath, string pattern = "*.yaml")
    {
        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"Configuration directory not found: {directoryPath}");
        }

        var allDefinitions = new List<EntityDefinition>();
        var files = Directory.GetFiles(directoryPath, pattern, SearchOption.AllDirectories);

        foreach (var file in files)
        {
            var definitions = await LoadFromFileAsync(file);
            allDefinitions.AddRange(definitions);
        }

        return allDefinitions;
    }

    /// <summary>
    /// Loads entity definitions from YAML content
    /// </summary>
    public List<EntityDefinition> LoadFromYaml(string yamlContent)
    {
        var config = _yamlDeserializer.Deserialize<EntityConfigurationRoot>(yamlContent);
        return config?.Entities ?? new List<EntityDefinition>();
    }

    /// <summary>
    /// Loads entity definitions from JSON content
    /// </summary>
    public List<EntityDefinition> LoadFromJson(string jsonContent)
    {
        var config = JsonSerializer.Deserialize<EntityConfigurationRoot>(jsonContent, _jsonOptions);
        return config?.Entities ?? new List<EntityDefinition>();
    }

    /// <summary>
    /// Validates entity definitions for correctness
    /// </summary>
    public List<string> ValidateDefinitions(IEnumerable<EntityDefinition> definitions)
    {
        var errors = new List<string>();
        var entityNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entity in definitions)
        {
            // Check required fields
            if (string.IsNullOrWhiteSpace(entity.Name))
            {
                errors.Add("Entity name is required");
                continue;
            }

            // Check for duplicates
            if (!entityNames.Add(entity.Name))
            {
                errors.Add($"Duplicate entity name: {entity.Name}");
            }

            // Validate name format
            if (!IsValidIdentifier(entity.Name))
            {
                errors.Add($"Invalid entity name '{entity.Name}': must be a valid C# identifier");
            }

            // Validate fields
            var fieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in entity.Fields)
            {
                if (string.IsNullOrWhiteSpace(field.Name))
                {
                    errors.Add($"Entity '{entity.Name}': Field name is required");
                    continue;
                }

                if (!fieldNames.Add(field.Name))
                {
                    errors.Add($"Entity '{entity.Name}': Duplicate field name: {field.Name}");
                }

                if (!IsValidIdentifier(field.Name))
                {
                    errors.Add($"Entity '{entity.Name}': Invalid field name '{field.Name}'");
                }

                // Validate reference fields
                if (field.Type == FieldType.Reference && string.IsNullOrWhiteSpace(field.ReferencesEntity))
                {
                    errors.Add($"Entity '{entity.Name}', field '{field.Name}': Reference field must specify ReferencesEntity");
                }
            }
        }

        return errors;
    }

    private static bool IsValidIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        if (!char.IsLetter(name[0]) && name[0] != '_')
            return false;

        return name.All(c => char.IsLetterOrDigit(c) || c == '_');
    }
}

/// <summary>
/// Root object for entity configuration files
/// </summary>
public class EntityConfigurationRoot
{
    /// <summary>
    /// List of entity definitions
    /// </summary>
    public List<EntityDefinition> Entities { get; set; } = new();
}
