namespace Hazina.AgentFactory.Services.Email;

/// <summary>
/// Interface for email operations.
/// </summary>
public interface IEmailService
{
    Task<bool> SendEmailAsync(string to, string subject, string body, CancellationToken cancel);
    Task<List<EmailSummary>> ListInboxEmailsAsync(int amount, bool oldestFirst, CancellationToken cancel);
    Task<EmailDetail?> ReadEmailAsync(string id, CancellationToken cancel);
    Task<string> CreateMailboxFolderAsync(string folderName, CancellationToken cancel);
    Task<string> MoveEmailToFolderAsync(string emailUid, string targetFolderName, CancellationToken cancel);
    Task<List<string>> ListMailboxFoldersAsync(CancellationToken cancel);
}
