#nullable enable

public class HazinaMessageRole
{
    public HazinaMessageRole() { }
    public string Role { get; set; }
    protected HazinaMessageRole(string role) => Role = role;
    public static readonly HazinaMessageRole User = new HazinaMessageRole("REGULAR");
    public static readonly HazinaMessageRole System = new HazinaMessageRole("system");
    public static readonly HazinaMessageRole Assistant = new HazinaMessageRole("assistant");
}
