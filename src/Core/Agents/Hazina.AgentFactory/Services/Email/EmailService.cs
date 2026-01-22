using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;

using MimeKit;

namespace Hazina.AgentFactory.Services.Email;

/// <summary>
/// Service for email operations including sending, reading, and organizing emails.
/// Extracted from AgentFactory to follow Single Responsibility Principle.
/// </summary>
public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;

    public EmailService(EmailSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public async Task<bool> SendEmailAsync(string to, string subject, string body, CancellationToken cancel)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Agent", _settings.SmtpUser));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, _settings.UseSsl, cancel);
        await client.AuthenticateAsync(_settings.SmtpUser, _settings.SmtpPassword, cancel);
        await client.SendAsync(message, cancel);
        await client.DisconnectAsync(true, cancel);

        // Also add to sent folder
        using var imap = new ImapClient();
        await imap.ConnectAsync(_settings.ImapHost, _settings.ImapPort, _settings.UseSsl, cancel);
        await imap.AuthenticateAsync(_settings.ImapUser, _settings.ImapPassword, cancel);

        var sent = imap.GetFolder(SpecialFolder.Sent) ?? imap.GetFolder("Sent");
        await sent.OpenAsync(FolderAccess.ReadWrite);
        await sent.AppendAsync(message);
        await imap.DisconnectAsync(true, cancel);

        return true;
    }

    public async Task<List<EmailSummary>> ListInboxEmailsAsync(int amount, bool oldestFirst, CancellationToken cancel)
    {
        using var client = new ImapClient();
        await client.ConnectAsync(_settings.ImapHost, _settings.ImapPort, _settings.UseSsl, cancel);
        await client.AuthenticateAsync(_settings.ImapUser, _settings.ImapPassword, cancel);

        var inbox = client.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadOnly, cancel);

        var uids = await inbox.SearchAsync(MailKit.Search.SearchQuery.All, cancel);
        uids = oldestFirst ? uids.Reverse().ToList() : uids;
        var lastUids = uids.Reverse().Take(amount).ToList();

        var summaries = await inbox.FetchAsync(lastUids, MessageSummaryItems.Envelope, cancel);

        var result = summaries
            .Select(summary => new EmailSummary
            {
                Id = summary.UniqueId.ToString(),
                Sender = summary.Envelope?.From?.Mailboxes?.FirstOrDefault()?.Address ?? "Unknown",
                Subject = summary.Envelope?.Subject ?? "(no subject)",
                Date = summary.Envelope?.Date?.DateTime ?? DateTime.MinValue
            })
            .OrderByDescending(e => e.Date)
            .ToList();

        await client.DisconnectAsync(true, cancel);
        return result;
    }

    public async Task<EmailDetail?> ReadEmailAsync(string id, CancellationToken cancel)
    {
        using var client = new ImapClient();
        await client.ConnectAsync(_settings.ImapHost, _settings.ImapPort, _settings.UseSsl, cancel);
        await client.AuthenticateAsync(_settings.ImapUser, _settings.ImapPassword, cancel);

        var inbox = client.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadOnly, cancel);

        if (!UniqueId.TryParse(id, out var uid))
            return null;

        var message = await inbox.GetMessageAsync(uid, cancel);
        var body = message.TextBody ?? message.HtmlBody ?? "<no content>";

        await client.DisconnectAsync(true, cancel);

        return new EmailDetail
        {
            Sender = message.From.Mailboxes.FirstOrDefault()?.Address ?? "Unknown",
            Subject = message.Subject,
            Body = body
        };
    }

    public async Task<string> CreateMailboxFolderAsync(string folderName, CancellationToken cancel)
    {
        using var client = new ImapClient();
        await client.ConnectAsync(_settings.ImapHost, _settings.ImapPort, _settings.UseSsl, cancel);
        await client.AuthenticateAsync(_settings.ImapUser, _settings.ImapPassword, cancel);

        var personal = client.GetFolder(client.PersonalNamespaces[0]);

        try
        {
            if (await personal.GetSubfolderAsync(folderName, cancel) != null)
                return $"Folder '{folderName}' already exists.";
        }
        catch (FolderNotFoundException)
        {
            // Expected: folder does not exist, so we can create it.
        }

        await personal.CreateAsync(folderName, true, cancel);

        await client.DisconnectAsync(true, cancel);
        return $"Folder '{folderName}' created successfully.";
    }

    public async Task<string> MoveEmailToFolderAsync(string emailUid, string targetFolderName, CancellationToken cancel)
    {
        using var client = new ImapClient();
        await client.ConnectAsync(_settings.ImapHost, _settings.ImapPort, _settings.UseSsl, cancel);
        await client.AuthenticateAsync(_settings.ImapUser, _settings.ImapPassword, cancel);

        var inbox = client.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadWrite, cancel);

        if (!UniqueId.TryParse(emailUid, out var uid))
            return "Invalid email UID.";

        var root = client.GetFolder(client.PersonalNamespaces[0]);
        var folders = await root.GetSubfoldersAsync(false, cancel);

        var dest = folders.FirstOrDefault(f => string.Equals(f.Name, targetFolderName, StringComparison.OrdinalIgnoreCase));
        if (dest == null)
            return $"Target folder '{targetFolderName}' does not exist.";

        await inbox.MoveToAsync(uid, dest, cancel);

        await client.DisconnectAsync(true, cancel);
        return $"Email moved to '{targetFolderName}'.";
    }

    public async Task<List<string>> ListMailboxFoldersAsync(CancellationToken cancel)
    {
        using var client = new ImapClient();
        await client.ConnectAsync(_settings.ImapHost, _settings.ImapPort, _settings.UseSsl, cancel);
        await client.AuthenticateAsync(_settings.ImapUser, _settings.ImapPassword, cancel);

        var root = await client.GetFolderAsync(client.PersonalNamespaces[0].Path, cancel);

        var all = new List<IMailFolder>();
        await EnumerateFoldersAsync(root, all, cancel, isRoot: true);

        await client.DisconnectAsync(true, cancel);

        return all.Select(f => f.FullName).ToList();
    }

    private async Task EnumerateFoldersAsync(IMailFolder folder, List<IMailFolder> list, CancellationToken cancel, bool isRoot = false)
    {
        list.Add(folder);

        if (!folder.Attributes.HasFlag(FolderAttributes.NoSelect))
        {
            try
            {
                await folder.OpenAsync(FolderAccess.ReadOnly, cancel);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not open folder '{folder.FullName}': {ex.Message}");
            }
        }

        foreach (var sub in await folder.GetSubfoldersAsync(false, cancel))
        {
            await EnumerateFoldersAsync(sub, list, cancel, false);
        }
    }
}
