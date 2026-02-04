using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.IO;

namespace Hazina.App.HazinaCoder.Core.Learning;

/// <summary>
/// Remembers user preferences, style, and past corrections.
/// Builds a relationship model over time to provide personalized assistance.
/// </summary>
/// <remarks>
/// Improvement #31: Relationship Memory
///
/// Tracks:
/// - Coding style preferences (tabs vs spaces, naming conventions)
/// - Communication preferences (verbosity, formality)
/// - Past corrections and feedback
/// - Domain knowledge and expertise areas
/// - Work patterns (best times, typical workflows)
/// </remarks>
public class RelationshipMemory
{
    private readonly string _storageFile;
    private UserProfile _profile = new();

    public RelationshipMemory(string storageFile)
    {
        _storageFile = storageFile;
        LoadProfile();
    }

    /// <summary>
    /// Record a user preference
    /// </summary>
    public void RecordPreference(string category, string key, string value, string context = "")
    {
        if (!_profile.Preferences.ContainsKey(category))
        {
            _profile.Preferences[category] = new Dictionary<string, PreferenceValue>();
        }

        _profile.Preferences[category][key] = new PreferenceValue
        {
            Value = value,
            Context = context,
            LastUpdated = DateTime.UtcNow,
            Confidence = 1.0
        };

        Console.WriteLine($"[Memory] Learned preference: {category}.{key} = {value}");
        SaveProfile();
    }

    /// <summary>
    /// Record a user correction
    /// </summary>
    public void RecordCorrection(string what, string from, string to, string reason = "")
    {
        _profile.Corrections.Add(new Correction
        {
            What = what,
            From = from,
            To = to,
            Reason = reason,
            Timestamp = DateTime.UtcNow
        });

        Console.WriteLine($"[Memory] Learned correction: {what} should be {to} (was {from})");
        SaveProfile();
    }

    /// <summary>
    /// Get a preference value
    /// </summary>
    public string? GetPreference(string category, string key)
    {
        if (_profile.Preferences.TryGetValue(category, out var categoryPrefs))
        {
            if (categoryPrefs.TryGetValue(key, out var pref))
            {
                return pref.Value;
            }
        }
        return null;
    }

    /// <summary>
    /// Get all preferences in a category
    /// </summary>
    public IReadOnlyDictionary<string, PreferenceValue>? GetPreferences(string category)
    {
        return _profile.Preferences.TryGetValue(category, out var prefs) ? prefs : null;
    }

    /// <summary>
    /// Find similar past corrections
    /// </summary>
    public IEnumerable<Correction> FindSimilarCorrections(string context)
    {
        // TODO: Use embeddings for semantic similarity
        return _profile.Corrections
            .Where(c => c.What.Contains(context, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(c => c.Timestamp)
            .Take(5);
    }

    /// <summary>
    /// Get user profile summary
    /// </summary>
    public UserProfile GetProfile() => _profile;

    private void SaveProfile()
    {
        try
        {
            var json = JsonSerializer.Serialize(_profile, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_storageFile, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Memory] ERROR: Failed to save profile: {ex.Message}");
        }
    }

    private void LoadProfile()
    {
        try
        {
            if (File.Exists(_storageFile))
            {
                var json = File.ReadAllText(_storageFile);
                var profile = JsonSerializer.Deserialize<UserProfile>(json);
                if (profile != null)
                {
                    _profile = profile;
                    Console.WriteLine($"[Memory] Loaded user profile ({_profile.Corrections.Count} corrections, {_profile.Preferences.Count} preference categories)");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Memory] ERROR: Failed to load profile: {ex.Message}");
        }
    }
}

public class UserProfile
{
    public Dictionary<string, Dictionary<string, PreferenceValue>> Preferences { get; set; } = new();
    public List<Correction> Corrections { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastInteraction { get; set; } = DateTime.UtcNow;
}

public class PreferenceValue
{
    public string Value { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
    public double Confidence { get; set; } = 1.0;
}

public class Correction
{
    public string What { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
