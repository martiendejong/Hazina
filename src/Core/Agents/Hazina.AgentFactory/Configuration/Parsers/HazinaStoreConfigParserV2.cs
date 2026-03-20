using Hazina.LLMs.Infrastructure;
using Hazina.AgentFactory.Configuration.Validation;

namespace Hazina.AgentFactory.Configuration.Parsers;

/// <summary>
/// Improved store config parser that separates parsing from validation
/// and uses dependency injection for testability.
/// </summary>
public class HazinaStoreConfigParserV2
{
    private readonly IFileSystem _fileSystem;

    public HazinaStoreConfigParserV2(IFileSystem? fileSystem = null)
    {
        _fileSystem = fileSystem ?? new PhysicalFileSystem();
    }

    /// <summary>
    /// Parses store configurations from .Hazina format with validation.
    /// Returns detailed diagnostics instead of throwing exceptions.
    /// </summary>
    public ConfigurationResult<List<StoreConfig>> Parse(string input)
    {
        var result = new ConfigurationResult<List<StoreConfig>>();
        var stores = new List<StoreConfig>();
        StoreConfig? current = null;
        int lineNumber = 0;

        try
        {
            var lines = input.Split(["\r\n", "\n"], StringSplitOptions.TrimEntries);

            foreach (var line in lines)
            {
                lineNumber++;
                var trimmed = line.Trim();

                if (string.IsNullOrEmpty(trimmed))
                {
                    if (current != null)
                    {
                        stores.Add(current);
                        current = null;
                    }
                    continue;
                }

                if (current == null)
                    current = new StoreConfig();

                var sepIdx = trimmed.IndexOf(':');
                if (sepIdx < 0)
                {
                    result.AddWarning($"Line {lineNumber}", $"Malformed line (missing colon): {trimmed}", "PARSE001");
                    continue;
                }

                var key = trimmed.Substring(0, sepIdx).Trim();
                var value = trimmed.Substring(sepIdx + 1).Trim();

                ParseField(current, key, value, result, lineNumber);
            }

            // Don't forget trailing store
            if (current != null)
                stores.Add(current);

            result.Value = stores;

            // Run validation on parsed stores
            var validationResult = StoreConfigValidator.ValidateAll(stores);
            result.Errors.AddRange(validationResult.Errors);
            result.Warnings.AddRange(validationResult.Warnings);
            result.Infos.AddRange(validationResult.Infos);

            return result;
        }
        catch (Exception ex)
        {
            result.AddError("Parse", $"Failed to parse configuration: {ex.Message}", "PARSE002");
            return result;
        }
    }

    private void ParseField(StoreConfig config, string key, string value, ConfigurationResult<List<StoreConfig>> result, int lineNumber)
    {
        switch (key)
        {
            case "Name":
                config.Name = value;
                break;

            case "Description":
                config.Description = value;
                break;

            case "Path":
                config.Path = value;
                break;

            case "FileFilters":
                config.FileFilters = string.IsNullOrWhiteSpace(value)
                    ? Array.Empty<string>()
                    : value.Split(',').Select(item => item.Trim()).ToArray();
                break;

            case "SubDirectory":
                config.SubDirectory = value;
                break;

            case "ExcludePattern":
                config.ExcludePattern = string.IsNullOrWhiteSpace(value)
                    ? Array.Empty<string>()
                    : value.Split(',').Select(item => item.Trim()).ToArray();
                break;

            case "IsReadOnly":
                if (bool.TryParse(value, out var isReadOnly))
                    config.IsReadOnly = isReadOnly;
                else
                    result.AddWarning($"Line {lineNumber}", $"Invalid boolean value for IsReadOnly: {value}", "PARSE003");
                break;

            case "ExplicitWriteEnabled":
                if (bool.TryParse(value, out var explicitWrite))
                    config.ExplicitWriteEnabled = explicitWrite;
                else
                    result.AddWarning($"Line {lineNumber}", $"Invalid boolean value for ExplicitWriteEnabled: {value}", "PARSE004");
                break;

            case "MaxFileSizeBytes":
                if (long.TryParse(value, out var maxSize))
                    config.MaxFileSizeBytes = maxSize;
                else
                    result.AddWarning($"Line {lineNumber}", $"Invalid number for MaxFileSizeBytes: {value}", "PARSE005");
                break;

            case "AllowedExtensions":
                config.AllowedExtensions = string.IsNullOrWhiteSpace(value)
                    ? null
                    : value.Split(',').Select(item => item.Trim()).ToArray();
                break;

            case "FollowSymlinks":
                if (bool.TryParse(value, out var followSymlinks))
                    config.FollowSymlinks = followSymlinks;
                else
                    result.AddWarning($"Line {lineNumber}", $"Invalid boolean value for FollowSymlinks: {value}", "PARSE006");
                break;

            case "MaxDirectoryDepth":
                if (int.TryParse(value, out var maxDepth))
                    config.MaxDirectoryDepth = maxDepth;
                else
                    result.AddWarning($"Line {lineNumber}", $"Invalid number for MaxDirectoryDepth: {value}", "PARSE007");
                break;

            default:
                result.AddWarning($"Line {lineNumber}", $"Unknown configuration key: {key}", "PARSE008");
                break;
        }
    }

    /// <summary>
    /// Serializes store configurations to .Hazina format.
    /// </summary>
    public string Serialize(IEnumerable<StoreConfig> stores)
    {
        var sb = new System.Text.StringBuilder();

        foreach (var store in stores)
        {
            sb.AppendLine($"Name: {store.Name}");
            sb.AppendLine($"Description: {store.Description}");
            sb.AppendLine($"Path: {store.Path}");
            sb.AppendLine($"FileFilters: {string.Join(",", store.FileFilters ?? Array.Empty<string>())}");
            sb.AppendLine($"SubDirectory: {store.SubDirectory}");
            sb.AppendLine($"ExcludePattern: {string.Join(",", store.ExcludePattern ?? Array.Empty<string>())}");
            sb.AppendLine($"IsReadOnly: {store.IsReadOnly}");
            sb.AppendLine($"ExplicitWriteEnabled: {store.ExplicitWriteEnabled}");

            if (store.MaxFileSizeBytes.HasValue)
                sb.AppendLine($"MaxFileSizeBytes: {store.MaxFileSizeBytes.Value}");

            if (store.AllowedExtensions != null && store.AllowedExtensions.Any())
                sb.AppendLine($"AllowedExtensions: {string.Join(",", store.AllowedExtensions)}");

            sb.AppendLine($"FollowSymlinks: {store.FollowSymlinks}");
            sb.AppendLine($"MaxDirectoryDepth: {store.MaxDirectoryDepth}");
            sb.AppendLine(); // Empty line between stores
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Loads and parses store configuration from a file.
    /// </summary>
    public ConfigurationResult<List<StoreConfig>> LoadFromFile(string path)
    {
        var result = new ConfigurationResult<List<StoreConfig>>();

        if (!_fileSystem.FileExists(path))
        {
            result.AddError("File", $"Configuration file not found: {path}", "FILE001");
            return result;
        }

        try
        {
            var content = _fileSystem.ReadAllText(path);
            return Parse(content);
        }
        catch (Exception ex)
        {
            result.AddError("File", $"Failed to read configuration file '{path}': {ex.Message}", "FILE002");
            return result;
        }
    }

    /// <summary>
    /// Saves store configuration to a file.
    /// </summary>
    public ConfigurationResult<bool> SaveToFile(IEnumerable<StoreConfig> stores, string path)
    {
        var result = new ConfigurationResult<bool>();

        try
        {
            // Validate before saving
            var storeList = stores.ToList();
            var validationResult = StoreConfigValidator.ValidateAll(storeList);

            if (!validationResult.IsValid)
            {
                result.AddError("Validation", "Cannot save invalid configuration", "SAVE001");
                result.Errors.AddRange(validationResult.Errors);
                return result;
            }

            // Ensure directory exists
            var directory = _fileSystem.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !_fileSystem.DirectoryExists(directory))
            {
                _fileSystem.CreateDirectory(directory);
            }

            var content = Serialize(storeList);
            _fileSystem.WriteAllText(path, content);

            result.Value = true;
            result.AddInfo("Save", $"Configuration saved successfully to {path}", "SAVE002");

            return result;
        }
        catch (Exception ex)
        {
            result.AddError("File", $"Failed to write configuration file '{path}': {ex.Message}", "FILE003");
            return result;
        }
    }
}
