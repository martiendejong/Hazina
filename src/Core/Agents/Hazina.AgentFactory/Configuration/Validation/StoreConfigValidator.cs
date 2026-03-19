namespace Hazina.AgentFactory.Configuration.Validation;

/// <summary>
/// Validates StoreConfig objects and surfaces detailed diagnostics.
/// </summary>
public class StoreConfigValidator
{
    private readonly ConfigurationResult<StoreConfig> _result;
    private readonly StoreConfig _config;

    public StoreConfigValidator(StoreConfig config)
    {
        _config = config;
        _result = new ConfigurationResult<StoreConfig> { Value = config };
    }

    /// <summary>
    /// Validates the store configuration and returns detailed diagnostics.
    /// </summary>
    public ConfigurationResult<StoreConfig> Validate()
    {
        ValidateRequiredFields();
        ValidatePath();
        ValidateFileFilters();
        ValidateSafetySettings();
        ValidateExcludePatterns();

        return _result;
    }

    private void ValidateRequiredFields()
    {
        if (string.IsNullOrWhiteSpace(_config.Name))
        {
            _result.AddError("Name", "Store name is required", "STORE001");
        }

        if (string.IsNullOrWhiteSpace(_config.Path))
        {
            _result.AddError("Path", "Store path is required", "STORE002");
        }
    }

    private void ValidatePath()
    {
        if (string.IsNullOrWhiteSpace(_config.Path))
            return;

        // Check for common path issues
        if (_config.Path.Contains("\\") && _config.Path.Contains("/"))
        {
            _result.AddWarning("Path", "Mixed path separators detected. Use Path.Combine for cross-platform compatibility", "STORE003");
        }

        // Check for absolute vs relative paths
        if (!Path.IsPathRooted(_config.Path))
        {
            _result.AddInfo("Path", "Using relative path. Ensure working directory is correct at runtime", "STORE004");
        }

        // Check for dangerous paths
        if (_config.Path.Contains(".."))
        {
            _result.AddWarning("Path", "Path contains parent directory references (..), ensure this is intentional", "STORE005");
        }

        // Check if path exists (non-blocking warning)
        try
        {
            var fullPath = Path.IsPathRooted(_config.Path) ? _config.Path : Path.GetFullPath(_config.Path);
            if (!Directory.Exists(fullPath) && !File.Exists(fullPath))
            {
                _result.AddWarning("Path", $"Path does not exist: {fullPath}", "STORE006");
            }
        }
        catch (Exception ex)
        {
            _result.AddWarning("Path", $"Could not validate path existence: {ex.Message}", "STORE007");
        }
    }

    private void ValidateFileFilters()
    {
        if (_config.FileFilters == null || !_config.FileFilters.Any())
        {
            _result.AddInfo("FileFilters", "No file filters specified, all files will be included", "STORE008");
            return;
        }

        foreach (var filter in _config.FileFilters)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                _result.AddWarning("FileFilters", "Empty file filter detected", "STORE009");
                continue;
            }

            // Check for common mistakes
            if (!filter.Contains("*") && !filter.Contains("?"))
            {
                _result.AddWarning("FileFilters", $"Filter '{filter}' has no wildcards, may not match any files", "STORE010");
            }

            // Check for overly broad filters
            if (filter == "*" || filter == "*.*")
            {
                _result.AddWarning("FileFilters", "Very broad file filter, consider being more specific for performance", "STORE011");
            }
        }
    }

    private void ValidateSafetySettings()
    {
        // Validate write access
        if (!_config.IsReadOnly && !_config.ExplicitWriteEnabled)
        {
            _result.AddWarning("Safety", "Write access requested but not explicitly enabled. Set ExplicitWriteEnabled=true", "STORE012");
        }

        // Validate size limits
        if (_config.MaxFileSizeBytes.HasValue)
        {
            if (_config.MaxFileSizeBytes.Value <= 0)
            {
                _result.AddError("MaxFileSizeBytes", "MaxFileSizeBytes must be positive", "STORE013");
            }
            else if (_config.MaxFileSizeBytes.Value < 1024)
            {
                _result.AddWarning("MaxFileSizeBytes", "MaxFileSizeBytes is very small (< 1KB)", "STORE014");
            }
            else if (_config.MaxFileSizeBytes.Value > 100 * 1024 * 1024) // 100MB
            {
                _result.AddWarning("MaxFileSizeBytes", "MaxFileSizeBytes is very large (> 100MB), may impact performance", "STORE015");
            }
        }

        // Validate extension restrictions
        if (_config.AllowedExtensions != null && _config.AllowedExtensions.Any())
        {
            foreach (var ext in _config.AllowedExtensions)
            {
                if (!ext.StartsWith("."))
                {
                    _result.AddWarning("AllowedExtensions", $"Extension '{ext}' should start with a dot (e.g., '.txt')", "STORE016");
                }
            }
        }

        // Check for dangerous extension combinations
        if (!_config.IsReadOnly && _config.AllowedExtensions != null)
        {
            var dangerousExtensions = new[] { ".exe", ".dll", ".bat", ".cmd", ".ps1", ".sh" };
            var hasDangerous = _config.AllowedExtensions.Any(ext =>
                dangerousExtensions.Contains(ext.ToLowerInvariant()));

            if (hasDangerous)
            {
                _result.AddWarning("AllowedExtensions", "Allowing executable extensions with write access is dangerous", "STORE017");
            }
        }
    }

    private void ValidateExcludePatterns()
    {
        if (_config.ExcludePattern == null || !_config.ExcludePattern.Any())
            return;

        foreach (var pattern in _config.ExcludePattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                _result.AddWarning("ExcludePattern", "Empty exclude pattern detected", "STORE018");
            }
        }

        // Suggest common exclusions if missing
        var commonExclusions = new[] { "node_modules", ".git", "bin", "obj", ".vs" };
        var hasCommonExclusions = commonExclusions.Any(common =>
            _config.ExcludePattern.Any(pattern => pattern.Contains(common)));

        if (!hasCommonExclusions)
        {
            _result.AddInfo("ExcludePattern", "Consider excluding common directories: node_modules, .git, bin, obj, .vs", "STORE019");
        }
    }

    /// <summary>
    /// Performs a comprehensive validation of a list of store configurations.
    /// </summary>
    public static ConfigurationResult<List<StoreConfig>> ValidateAll(List<StoreConfig> stores)
    {
        var result = new ConfigurationResult<List<StoreConfig>> { Value = stores };

        if (stores == null || !stores.Any())
        {
            result.AddWarning("Stores", "No stores configured", "STORE020");
            return result;
        }

        // Check for duplicate names
        var duplicateNames = stores
            .GroupBy(s => s.Name)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (var dupName in duplicateNames)
        {
            result.AddError("Stores", $"Duplicate store name: {dupName}", "STORE021");
        }

        // Validate each store individually
        for (int i = 0; i < stores.Count; i++)
        {
            var storeResult = new StoreConfigValidator(stores[i]).Validate();

            // Copy diagnostics with store index prefix
            foreach (var error in storeResult.Errors)
            {
                error.Field = $"Stores[{i}].{error.Field}";
                result.Errors.Add(error);
            }

            foreach (var warning in storeResult.Warnings)
            {
                warning.Field = $"Stores[{i}].{warning.Field}";
                result.Warnings.Add(warning);
            }

            foreach (var info in storeResult.Infos)
            {
                info.Field = $"Stores[{i}].{info.Field}";
                result.Infos.Add(info);
            }
        }

        return result;
    }
}
