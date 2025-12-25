namespace HtmlMockupGenerator.Models;

public class AppSettings
{
    public bool RequireAuthentication { get; set; } = true;
    public int DailyGenerationLimit { get; set; } = 10;
} 