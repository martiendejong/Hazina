using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Hazina.Tools.Models
{
    public class TypographyEntry
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string FontFamily { get; set; } = "Inter";
        public string FontSize { get; set; } = "16px";
        public string FontWeight { get; set; } = "400";
        public string LineHeight { get; set; } = "1.5";
        public string LetterSpacing { get; set; } = "0";
        public string TextTransform { get; set; } = "none";
    }

    /// <summary>
    /// Typography configuration for brand visual identity.
    /// Contains font pairings for different text roles (headings, body, accent, etc.).
    /// </summary>
    public class Typography : ChatResponse<Typography>
    {
        public Typography()
        {
        }

        [JsonPropertyName("Typography")]
        public List<TypographyEntry> Fonts { get; set; } = new List<TypographyEntry>();

        [JsonIgnore]
        public override Typography _example => new Typography
        {
            Fonts = new List<TypographyEntry>
            {
                new TypographyEntry
                {
                    Key = "heading-1",
                    Name = "Heading 1",
                    FontFamily = "Playfair Display",
                    FontSize = "48px",
                    FontWeight = "700",
                    LineHeight = "1.2",
                    LetterSpacing = "-0.025em",
                    TextTransform = "none"
                },
                new TypographyEntry
                {
                    Key = "heading-2",
                    Name = "Heading 2",
                    FontFamily = "Playfair Display",
                    FontSize = "36px",
                    FontWeight = "600",
                    LineHeight = "1.25",
                    LetterSpacing = "-0.025em",
                    TextTransform = "none"
                },
                new TypographyEntry
                {
                    Key = "body",
                    Name = "Body Text",
                    FontFamily = "Inter",
                    FontSize = "16px",
                    FontWeight = "400",
                    LineHeight = "1.625",
                    LetterSpacing = "0",
                    TextTransform = "none"
                }
            }
        };

        [JsonIgnore]
        public override string _signature => "{ Typography: [{ Key: string, Name: string, FontFamily: string, FontSize: string, FontWeight: string, LineHeight?: string, LetterSpacing?: string, TextTransform?: string }] }";
    }
}
