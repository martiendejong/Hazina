using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Hazina.App.HazinaCoder.Core.Skills;

/// <summary>
/// Discovers skills from markdown files with YAML frontmatter.
/// Scans configured directories at startup and registers all found skills.
/// </summary>
/// <remarks>
/// Improvement #21: Auto-Discoverable Skills
/// </remarks>
public class SkillDiscovery
{
    private readonly List<string> _skillDirectories;
    private readonly Dictionary<string, SkillMetadata> _skills = new();
    private readonly IDeserializer _yamlDeserializer;

    public SkillDiscovery(IEnumerable<string> skillDirectories)
    {
        _skillDirectories = skillDirectories.ToList();
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
    }

    /// <summary>
    /// Scan all configured directories and discover skills
    /// </summary>
    public int DiscoverSkills()
    {
        _skills.Clear();
        int count = 0;

        foreach (var directory in _skillDirectories)
        {
            if (!Directory.Exists(directory))
            {
                Console.WriteLine($"[SkillDiscovery] Directory not found: {directory}");
                continue;
            }

            var markdownFiles = Directory.GetFiles(directory, "*.md", SearchOption.AllDirectories);
            foreach (var file in markdownFiles)
            {
                try
                {
                    var skill = ParseSkillFile(file);
                    if (skill != null)
                    {
                        _skills[skill.Name] = skill;
                        count++;
                        Console.WriteLine($"[SkillDiscovery] Found skill: {skill.Name} ({file})");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SkillDiscovery] Error parsing {file}: {ex.Message}");
                }
            }
        }

        Console.WriteLine($"[SkillDiscovery] Discovered {count} skills");
        return count;
    }

    /// <summary>
    /// Parse a markdown file with YAML frontmatter into a skill
    /// </summary>
    private SkillMetadata? ParseSkillFile(string filePath)
    {
        var content = File.ReadAllText(filePath);

        // Check for YAML frontmatter (starts with ---)
        if (!content.StartsWith("---"))
            return null;

        var parts = content.Split(new[] { "---" }, 3, StringSplitOptions.None);
        if (parts.Length < 3)
            return null;

        var yamlContent = parts[1].Trim();
        var markdownContent = parts[2].Trim();

        // TODO: Proper YAML deserialization with YamlDotNet
        // For now, create a basic skill metadata
        var skill = new SkillMetadata
        {
            FilePath = filePath,
            Documentation = markdownContent,
            Name = Path.GetFileNameWithoutExtension(filePath)
        };

        // TODO: Parse YAML frontmatter properly
        // skill = _yamlDeserializer.Deserialize<SkillMetadata>(yamlContent);

        return skill;
    }

    /// <summary>
    /// Get all discovered skills
    /// </summary>
    public IReadOnlyDictionary<string, SkillMetadata> GetSkills() => _skills;

    /// <summary>
    /// Find skills by trigger keyword
    /// </summary>
    public IEnumerable<SkillMetadata> FindByTrigger(string trigger)
    {
        return _skills.Values.Where(s =>
            s.Triggers.Any(t => t.Equals(trigger, StringComparison.OrdinalIgnoreCase)));
    }
}
