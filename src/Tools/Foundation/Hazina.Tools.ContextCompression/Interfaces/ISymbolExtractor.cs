namespace Hazina.Tools.ContextCompression.Interfaces;

/// <summary>
/// Interface for extracting symbols from code
/// </summary>
public interface ISymbolExtractor
{
    /// <summary>
    /// Extract symbols from a file
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <returns>Dictionary of symbol names to locations</returns>
    Task<Dictionary<string, string>> ExtractSymbolsAsync(string filePath);

    /// <summary>
    /// Extract symbols from code content
    /// </summary>
    /// <param name="code">Code content</param>
    /// <param name="language">Programming language</param>
    /// <returns>Dictionary of symbol names to line numbers</returns>
    Task<Dictionary<string, int>> ExtractSymbolsFromCodeAsync(string code, string language);
}
