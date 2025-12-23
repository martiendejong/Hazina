using System.Text.Json.Serialization;

namespace HtmlMockupGenerator.Pages;

public class HtmlReplaceResponse : ChatResponse<HtmlReplaceResponse>
{
    public int StartCharIndex { get; set; }
    public int EndCharIndex { get; set; }
    public string ReplacementHTML { get; set; }

    [JsonIgnore]
    public override HtmlReplaceResponse _example { get => new HtmlReplaceResponse() { StartCharIndex = 121, EndCharIndex = 180 }; }

    [JsonIgnore]
    public override string _signature { get => "{ StartCharIndex: number, EndCharIndex: number, ReplacementHTML: string }"; }
}
