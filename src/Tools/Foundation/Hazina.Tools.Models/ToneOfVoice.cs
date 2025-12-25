using System.Collections.Generic;

namespace Hazina.Tools.Models;

public class ToneOfVoice
{
    public List<string> ToneOfVoiceDescriptors { get; set; } = new List<string>();

    public ToneOfVoice _example => new ToneOfVoice
    {
        ToneOfVoiceDescriptors = new List<string> { "Friendly", "Confident", "Professional" }
    };

    public string _signature => "{ ToneOfVoiceDescriptors: string[] }";
}

public class CoreValues
{
    public List<CoreValue> Values { get; set; } = new List<CoreValue>();

    public CoreValues _example => new CoreValues
    {
        Values = new List<CoreValue>
        {
            new CoreValue { Title = "Integrity", Description = "We act with honesty and strong moral principles" },
            new CoreValue { Title = "Innovation", Description = "We embrace creative solutions" }
        }
    };

    public string _signature => "{ Values: [{ Title: string, Description: string }] }";
}

public class CoreValue
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
