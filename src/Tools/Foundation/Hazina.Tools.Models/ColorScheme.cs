using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Hazina.Tools.Models
{
    public class ColorSchemeEntry
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = "#111827";
    }

    /// <summary>
    /// Color scheme configuration for brand visual identity.
    /// Contains primary, secondary, accent, and other brand colors.
    /// </summary>
    public class ColorScheme : ChatResponse<ColorScheme>
    {
        public ColorScheme()
        {
        }

        public List<ColorSchemeEntry> Colors { get; set; } = new List<ColorSchemeEntry>();

        [JsonIgnore]
        public override ColorScheme _example => new ColorScheme
        {
            Colors = new List<ColorSchemeEntry>
            {
                new ColorSchemeEntry
                {
                    Key = "primary",
                    Name = "Primary",
                    Color = "#d97706"
                },
                new ColorSchemeEntry
                {
                    Key = "secondary",
                    Name = "Secondary",
                    Color = "#1e293b"
                },
                new ColorSchemeEntry
                {
                    Key = "accent",
                    Name = "Accent",
                    Color = "#fbbf24"
                },
                new ColorSchemeEntry
                {
                    Key = "background",
                    Name = "Background",
                    Color = "#ffffff"
                },
                new ColorSchemeEntry
                {
                    Key = "text",
                    Name = "Text",
                    Color = "#0f172a"
                }
            }
        };

        [JsonIgnore]
        public override string _signature => "{ Colors: [{ Key: string, Name: string, Color: string }] }";
    }
}
