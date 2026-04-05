/// <summary>
/// Configurable safety policies for UpdateStore file operations.
/// Enforces constraints on file size and allowed extensions to prevent
/// accidental or malicious writes by AI-generated responses.
/// </summary>
public class StoreSafetyPolicy
{
    /// <summary>
    /// Maximum allowed file size in bytes. Default is 10 MB (10,485,760 bytes).
    /// Set to 0 or negative to disable the file size check.
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024; // 10 MB

    /// <summary>
    /// Set of allowed file extensions (including the leading dot, e.g. ".json").
    /// If empty, all extensions are allowed.
    /// Comparison is case-insensitive.
    /// </summary>
    public HashSet<string> AllowedExtensions { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ".json", ".md", ".txt", ".cs", ".ts", ".js",
        ".html", ".css", ".xml", ".yaml", ".yml",
        ".csv", ".svg", ".razor", ".csproj", ".sln",
        ".props", ".targets", ".config", ".gitignore"
    };

    /// <summary>
    /// Whether the safety policy is enabled. Default is true.
    /// When disabled, no validation is performed.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Validates a file modification against the safety policy.
    /// </summary>
    /// <param name="path">The relative file path being written.</param>
    /// <param name="content">The content to be written.</param>
    /// <exception cref="StoreSafetyPolicyViolationException">
    /// Thrown when the file modification violates the safety policy.
    /// </exception>
    public void Validate(string path, string content)
    {
        if (!Enabled) return;

        ValidateExtension(path);
        ValidateFileSize(path, content);
    }

    /// <summary>
    /// Validates a file move against the safety policy (checks destination extension).
    /// </summary>
    /// <param name="newPath">The destination file path.</param>
    /// <exception cref="StoreSafetyPolicyViolationException">
    /// Thrown when the move destination violates the extension allowlist.
    /// </exception>
    public void ValidateMove(string newPath)
    {
        if (!Enabled) return;

        ValidateExtension(newPath);
    }

    private void ValidateExtension(string path)
    {
        if (AllowedExtensions.Count == 0) return;

        var extension = Path.GetExtension(path);
        if (string.IsNullOrEmpty(extension))
        {
            // Files without extensions (e.g. Dockerfile, .gitignore) are allowed
            return;
        }

        if (!AllowedExtensions.Contains(extension))
        {
            throw new StoreSafetyPolicyViolationException(
                $"File extension '{extension}' is not in the allowed extensions list. " +
                $"File: '{path}'. Allowed extensions: {string.Join(", ", AllowedExtensions.Order())}");
        }
    }

    private void ValidateFileSize(string path, string content)
    {
        if (MaxFileSizeBytes <= 0) return;

        var sizeInBytes = System.Text.Encoding.UTF8.GetByteCount(content);
        if (sizeInBytes > MaxFileSizeBytes)
        {
            throw new StoreSafetyPolicyViolationException(
                $"File size ({sizeInBytes:N0} bytes) exceeds the maximum allowed size ({MaxFileSizeBytes:N0} bytes). " +
                $"File: '{path}'.");
        }
    }
}
