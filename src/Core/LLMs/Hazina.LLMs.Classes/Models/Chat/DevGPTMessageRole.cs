#nullable enable

public class DevGPTMessageRole
{
    public DevGPTMessageRole() { }
    public string Role { get; set; }
    protected DevGPTMessageRole(string role) => Role = role;
    public static readonly DevGPTMessageRole User = new DevGPTMessageRole("REGULAR");
    public static readonly DevGPTMessageRole System = new DevGPTMessageRole("system");
    public static readonly DevGPTMessageRole Assistant = new DevGPTMessageRole("assistant");
}
