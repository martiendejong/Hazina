namespace DevGPT.GenerationTools.Services.DataGathering.Abstractions;

/// <summary>
/// Notifier for real-time analysis field updates.
/// </summary>
public interface IAnalysisFieldNotifier
{
    /// <summary>
    /// Notifies clients that an analysis field was generated/updated.
    /// </summary>
    Task NotifyFieldGeneratedAsync(
        string projectId,
        string chatId,
        string key,
        string displayName,
        string content,
        string? feedback = null,
        string? componentName = null,
        CancellationToken cancellationToken = default);
}
