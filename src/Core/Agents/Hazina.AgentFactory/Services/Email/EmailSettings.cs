namespace Hazina.AgentFactory.Services.Email;

/// <summary>
/// Configuration settings for email operations via SMTP and IMAP.
/// </summary>
public class EmailSettings
{
    public string SmtpHost { get; set; } = "mail.zxcs.nl";
    public int SmtpPort { get; set; } = 587;
    public string SmtpUser { get; set; } = "info@martiendejong.nl";
    public string SmtpPassword { get; set; } = "hLPFy6MdUnfEDbYTwXps";
    public string ImapHost { get; set; } = "mail.zxcs.nl";
    public int ImapPort { get; set; } = 993;
    public string ImapUser { get; set; } = "info@martiendejong.nl";
    public string ImapPassword { get; set; } = "hLPFy6MdUnfEDbYTwXps";
    public bool UseSsl { get; set; } = true;
}
