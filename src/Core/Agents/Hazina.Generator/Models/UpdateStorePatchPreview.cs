/// <summary>
/// Preview of what would be changed by an UpdateStore operation, without applying changes.
/// Use DryRunUpdateStore() to get this preview before committing destructive changes.
/// </summary>
public class UpdateStorePatchPreview
{
    /// <summary>
    /// Files that would be modified (path → new content).
    /// </summary>
    public List<PatchModification> Modifications { get; set; } = new();

    /// <summary>
    /// Files that would be deleted.
    /// </summary>
    public List<string> Deletions { get; set; } = new();

    /// <summary>
    /// Files that would be moved (old path → new path).
    /// </summary>
    public List<PatchMove> Moves { get; set; } = new();

    /// <summary>
    /// Message from the LLM explaining the proposed changes.
    /// </summary>
    public string ResponseMessage { get; set; } = "";

    /// <summary>
    /// Total number of proposed changes.
    /// </summary>
    public int TotalChanges => Modifications.Count + Deletions.Count + Moves.Count;

    /// <summary>
    /// Whether there are any proposed changes.
    /// </summary>
    public bool HasChanges => TotalChanges > 0;

    /// <summary>
    /// Human-readable summary of the proposed patch.
    /// </summary>
    public string Summary =>
        $"{Modifications.Count} modification(s), {Deletions.Count} deletion(s), {Moves.Count} move(s)";
}

/// <summary>
/// A proposed file modification in a dry-run patch preview.
/// </summary>
public class PatchModification
{
    /// <summary>
    /// Relative file path within the store.
    /// </summary>
    public string Path { get; set; } = "";

    /// <summary>
    /// Name or title of the document.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Full proposed new content of the file.
    /// </summary>
    public string Contents { get; set; } = "";
}

/// <summary>
/// A proposed file move in a dry-run patch preview.
/// </summary>
public class PatchMove
{
    /// <summary>
    /// Current relative file path.
    /// </summary>
    public string Path { get; set; } = "";

    /// <summary>
    /// New relative file path after the move.
    /// </summary>
    public string NewPath { get; set; } = "";
}
