#nullable enable

/// <summary>
/// Represents the role of a message in a chat conversation.
/// </summary>
public class HazinaMessageRole
{
    /// <summary>
    /// Initializes a new instance of the HazinaMessageRole class.
    /// </summary>
    public HazinaMessageRole() { }

    /// <summary>
    /// Gets or sets the role identifier.
    /// </summary>
    public string Role { get; set; } = "";

    /// <summary>
    /// Initializes a new instance with the specified role.
    /// </summary>
    /// <param name="role">The role identifier.</param>
    protected HazinaMessageRole(string role) => Role = role;

    /// <summary>
    /// Represents a user role.
    /// </summary>
    public static readonly HazinaMessageRole User = new HazinaMessageRole("REGULAR");

    /// <summary>
    /// Represents a system role.
    /// </summary>
    public static readonly HazinaMessageRole System = new HazinaMessageRole("system");

    /// <summary>
    /// Represents an assistant role.
    /// </summary>
    public static readonly HazinaMessageRole Assistant = new HazinaMessageRole("assistant");
}
