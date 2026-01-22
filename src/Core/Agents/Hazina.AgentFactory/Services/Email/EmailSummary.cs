namespace Hazina.AgentFactory.Services.Email;

/// <summary>
/// Summary information about an email message.
/// </summary>
public class EmailSummary
{
    public string Id { get; set; } = string.Empty;
    public string Sender { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public DateTime Date { get; set; }
}
