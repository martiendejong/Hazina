using System.ComponentModel.DataAnnotations;

namespace HtmlMockupGenerator.Models;

public class User
{
    [Key]
    public string Id { get; set; } = string.Empty;
    
    [Required]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    public string Name { get; set; } = string.Empty;
    
    public string? Picture { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime LastLoginAt { get; set; } = DateTime.UtcNow;
    
    // Daily generation tracking
    public DateTime LastGenerationDate { get; set; } = DateTime.MinValue;
    
    public int GenerationsToday { get; set; } = 0;
    
    public const int DailyLimit = 10;
    
    public bool CanGenerateToday()
    {
        var today = DateTime.UtcNow.Date;
        if (LastGenerationDate.Date != today)
        {
            return true; // New day, reset counter
        }
        return GenerationsToday < DailyLimit;
    }
    
    public void IncrementGenerationCount()
    {
        var today = DateTime.UtcNow.Date;
        if (LastGenerationDate.Date != today)
        {
            GenerationsToday = 1;
            LastGenerationDate = today;
        }
        else
        {
            GenerationsToday++;
        }
    }
    
    public int RemainingGenerationsToday()
    {
        var today = DateTime.UtcNow.Date;
        if (LastGenerationDate.Date != today)
        {
            return DailyLimit;
        }
        return Math.Max(0, DailyLimit - GenerationsToday);
    }
} 