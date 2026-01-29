namespace Hazina.API.Generic.Configuration;

/// <summary>
/// Defines an entity via configuration (YAML/JSON).
/// This allows non-developers to define new entities without writing C# code.
/// </summary>
public class EntityDefinition
{
    /// <summary>
    /// The name of the entity (used in routes and table names)
    /// Example: "Product", "BlogPost", "Customer"
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional plural name for list routes. If not set, adds 's' to Name.
    /// Example: "Products", "BlogPosts", "Customers"
    /// </summary>
    public string? PluralName { get; set; }

    /// <summary>
    /// Description shown in API documentation
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The fields/properties of this entity
    /// </summary>
    public List<FieldDefinition> Fields { get; set; } = new();

    /// <summary>
    /// Which features are enabled for this entity
    /// </summary>
    public EntityFeatures Features { get; set; } = new();

    /// <summary>
    /// Access control settings
    /// </summary>
    public AccessControl Access { get; set; } = new();

    /// <summary>
    /// Gets the plural name, generating one if not explicitly set
    /// </summary>
    public string GetPluralName() =>
        !string.IsNullOrEmpty(PluralName) ? PluralName :
        Name.EndsWith("y") ? Name[..^1] + "ies" :
        Name.EndsWith("s") ? Name + "es" :
        Name + "s";
}

/// <summary>
/// Defines a field/property on an entity
/// </summary>
public class FieldDefinition
{
    /// <summary>
    /// Field name (will be used as property name)
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Data type of the field
    /// </summary>
    public FieldType Type { get; set; } = FieldType.String;

    /// <summary>
    /// Whether this field is required
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Maximum length for string fields
    /// </summary>
    public int? MaxLength { get; set; }

    /// <summary>
    /// Minimum value for numeric fields
    /// </summary>
    public decimal? MinValue { get; set; }

    /// <summary>
    /// Maximum value for numeric fields
    /// </summary>
    public decimal? MaxValue { get; set; }

    /// <summary>
    /// Default value (as string, will be parsed)
    /// </summary>
    public string? DefaultValue { get; set; }

    /// <summary>
    /// Description for API documentation
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether this field should be included in search/embedding
    /// </summary>
    public bool Searchable { get; set; }

    /// <summary>
    /// Whether this field should be indexed in the database
    /// </summary>
    public bool Indexed { get; set; }

    /// <summary>
    /// For reference fields: the name of the related entity
    /// </summary>
    public string? ReferencesEntity { get; set; }
}

/// <summary>
/// Supported field types
/// </summary>
public enum FieldType
{
    String,
    Text,           // Long text
    Int,
    Long,
    Decimal,
    Double,
    Bool,
    DateTime,
    DateOnly,
    TimeOnly,
    Guid,
    Json,           // Stored as JSON string
    Enum,           // Stored as string
    Reference,      // Foreign key to another entity
    List,           // Collection (stored as JSON)
    Binary          // Byte array (files, etc.)
}

/// <summary>
/// Feature flags for an entity
/// </summary>
public class EntityFeatures
{
    /// <summary>
    /// Enable CRUD operations (Create, Read, Update, Delete)
    /// </summary>
    public bool Crud { get; set; } = true;

    /// <summary>
    /// Enable full-text search
    /// </summary>
    public bool Search { get; set; }

    /// <summary>
    /// Enable RAG embedding generation
    /// </summary>
    public bool Embedding { get; set; }

    /// <summary>
    /// Enable soft delete instead of hard delete
    /// </summary>
    public bool SoftDelete { get; set; } = true;

    /// <summary>
    /// Track creation and modification timestamps
    /// </summary>
    public bool Timestamps { get; set; } = true;

    /// <summary>
    /// Enable list with pagination
    /// </summary>
    public bool Pagination { get; set; } = true;

    /// <summary>
    /// Enable filtering via query parameters
    /// </summary>
    public bool Filtering { get; set; } = true;

    /// <summary>
    /// Enable sorting via query parameters
    /// </summary>
    public bool Sorting { get; set; } = true;

    /// <summary>
    /// Enable bulk operations (create/update/delete many)
    /// </summary>
    public bool BulkOperations { get; set; }

    /// <summary>
    /// Enable export to CSV/Excel
    /// </summary>
    public bool Export { get; set; }

    /// <summary>
    /// Enable real-time updates via SignalR
    /// </summary>
    public bool Realtime { get; set; }
}

/// <summary>
/// Access control settings for an entity
/// </summary>
public class AccessControl
{
    /// <summary>
    /// Whether authentication is required to access this entity
    /// </summary>
    public bool RequiresAuth { get; set; } = true;

    /// <summary>
    /// Roles that can read this entity (empty = all authenticated users)
    /// </summary>
    public List<string> ReadRoles { get; set; } = new();

    /// <summary>
    /// Roles that can create this entity
    /// </summary>
    public List<string> CreateRoles { get; set; } = new();

    /// <summary>
    /// Roles that can update this entity
    /// </summary>
    public List<string> UpdateRoles { get; set; } = new();

    /// <summary>
    /// Roles that can delete this entity
    /// </summary>
    public List<string> DeleteRoles { get; set; } = new();

    /// <summary>
    /// Whether users can only access their own entities
    /// </summary>
    public bool OwnerOnly { get; set; }

    /// <summary>
    /// Whether access is scoped to tenant
    /// </summary>
    public bool TenantScoped { get; set; }
}
