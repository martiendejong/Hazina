namespace Hazina.LongContext.Models;

/// <summary>
/// Unique identifier for a long-context query session.
/// Used to track multi-turn recursive queries and associate them with memory/history.
/// </summary>
public record LongContextSessionId(Guid Value)
{
    public static LongContextSessionId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}
