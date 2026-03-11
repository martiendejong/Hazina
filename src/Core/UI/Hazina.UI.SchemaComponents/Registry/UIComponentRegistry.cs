using System.Reflection;
using System.Text.Json;
using Hazina.UI.SchemaComponents.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Hazina.UI.SchemaComponents.Registry;

/// <summary>
/// In-memory registry for UI component definitions.
/// Loads built-in components from embedded YAML resources.
/// </summary>
public class UIComponentRegistry : IUIComponentRegistry
{
    private readonly Dictionary<string, UIComponentDefinition> _components = new();
    private readonly IDeserializer _yamlDeserializer;

    public UIComponentRegistry()
    {
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        LoadBuiltInComponents();
    }

    /// <summary>
    /// Load built-in components from embedded .component.yaml resources.
    /// </summary>
    private void LoadBuiltInComponents()
    {
        var assembly = typeof(UIComponentRegistry).Assembly;
        var resources = assembly.GetManifestResourceNames()
            .Where(n => n.EndsWith(".component.yaml"))
            .ToList();

        foreach (var resource in resources)
        {
            try
            {
                using var stream = assembly.GetManifestResourceStream(resource);
                if (stream == null) continue;

                using var reader = new StreamReader(stream);
                var yaml = reader.ReadToEnd();

                // Deserialize YAML to intermediate object
                var componentData = _yamlDeserializer.Deserialize<Dictionary<string, object>>(yaml);

                var definition = ParseComponentDefinition(componentData);
                _components[definition.Id] = definition;
            }
            catch (Exception ex)
            {
                // Log error but continue loading other components
                Console.WriteLine($"Warning: Failed to load component from {resource}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Parse component definition from YAML-deserialized data.
    /// </summary>
    private UIComponentDefinition ParseComponentDefinition(Dictionary<string, object> data)
    {
        var id = data["id"].ToString()!;
        var type = data["type"].ToString()!;
        var category = data["category"].ToString()!;
        var description = data.ContainsKey("description") ? data["description"].ToString() : null;

        // Convert schema object to JsonDocument
        var schemaJson = JsonSerializer.Serialize(data["schema"]);
        var schema = JsonDocument.Parse(schemaJson);

        // Parse validation rules if present
        ValidationRules? validation = null;
        if (data.ContainsKey("validation"))
        {
            var validationData = data["validation"] as Dictionary<object, object>;
            if (validationData?.ContainsKey("rules") == true)
            {
                var rulesJson = JsonSerializer.Serialize(validationData["rules"]);
                var rules = JsonSerializer.Deserialize<List<ValidationRule>>(rulesJson);
                validation = new ValidationRules { Rules = rules ?? new List<ValidationRule>() };
            }
        }

        // Parse security rules
        var securityData = data.ContainsKey("security")
            ? data["security"] as Dictionary<object, object>
            : null;

        var security = new SecurityRules
        {
            RequiresApproval = securityData?.ContainsKey("requiresApproval") == true
                ? Convert.ToBoolean(securityData["requiresApproval"])
                : false,
            AllowedOperations = securityData?.ContainsKey("allowedOperations") == true
                ? ((List<object>)securityData["allowedOperations"]).Select(o => o.ToString()!).ToList()
                : new List<string> { "view" },
            RiskLevel = securityData?.ContainsKey("riskLevel") == true
                ? securityData["riskLevel"].ToString()!
                : "read"
        };

        return new UIComponentDefinition
        {
            Id = id,
            Type = type,
            Category = category,
            Description = description,
            Schema = schema,
            Validation = validation,
            Security = security
        };
    }

    public UIComponentDefinition GetComponent(string componentId)
    {
        if (_components.TryGetValue(componentId, out var component))
        {
            return component;
        }

        throw new KeyNotFoundException($"Component '{componentId}' not found in registry.");
    }

    public IEnumerable<UIComponentDefinition> GetByCategory(string category)
    {
        return _components.Values.Where(c => c.Category == category);
    }

    public bool ValidateComponentState(string componentId, JsonElement state)
    {
        var component = GetComponent(componentId);

        // TODO: Implement JSON Schema validation using NJsonSchema
        // For now, return true (validation implementation in future iteration)
        return true;
    }

    public UIComponentDefinition RegisterCustomComponent(UIComponentDefinition definition)
    {
        if (_components.ContainsKey(definition.Id))
        {
            throw new InvalidOperationException($"Component '{definition.Id}' already registered.");
        }

        _components[definition.Id] = definition;
        return definition;
    }

    public IEnumerable<string> GetAllComponentIds()
    {
        return _components.Keys;
    }
}
