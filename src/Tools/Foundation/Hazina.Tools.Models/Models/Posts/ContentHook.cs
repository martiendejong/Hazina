using DevGPT.GenerationTools.Models;
using DevGPT.GenerationTools.Models.WordPress.Blogs;

public class ContentHook : Serializer<ContentHook>
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Reason { get; set; }
    public string Prompt { get; set; } = "Generate 9 examples for the following content type.\nName:\n{naam}\nDescription:\n{description}\n\nWhy:\n{reason}";
    public bool? Like { get; set; }

    public string GetPreparedPrompt()
    {
        return Prompt.Replace("{reason}", Reason).Replace("{description}", Description).Replace("{naam}", Name);
    }
}

