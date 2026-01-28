namespace Hazina.AgentFactory.Services.Email;

/// <summary>
/// Detailed information about an email message including body content.
/// </summary>
public class EmailDetail
{
    public string Sender { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}
