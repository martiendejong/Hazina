namespace Hazina.AI.RAG.Graph.Models;

/// <summary>
/// Defines the schema/ontology for the knowledge graph.
/// </summary>
public class GraphSchema
{
    public List<string> EntityTypes { get; set; } = new() { "Person", "Organization", "Location", "Concept", "Event", "Product", "Document", "Topic" };
    public List<string> RelationTypes { get; set; } = new() { "WORKS_FOR", "LOCATED_IN", "CREATED_BY", "INFLUENCED_BY", "PART_OF", "MEMBER_OF", "RELATED_TO" };
    public Dictionary<string, List<string>> AllowedRelations { get; set; } = new();
    public Dictionary<string, string> EntityHierarchy { get; set; } = new();
    public Dictionary<string, string> RelationHierarchy { get; set; } = new();
    public bool StrictValidation { get; set; } = false;
    
    public bool IsValidEntityType(string entityType) => !StrictValidation || EntityTypes.Count == 0 || EntityTypes.Contains(entityType);
    public bool IsValidRelationType(string relationType) => !StrictValidation || RelationTypes.Count == 0 || RelationTypes.Contains(relationType);
    
    public static GraphSchema CreatePermissive() => new GraphSchema { StrictValidation = false, EntityTypes = new(), RelationTypes = new(), AllowedRelations = new() };
    public static GraphSchema CreateDefault() => new GraphSchema { StrictValidation = true };
}
